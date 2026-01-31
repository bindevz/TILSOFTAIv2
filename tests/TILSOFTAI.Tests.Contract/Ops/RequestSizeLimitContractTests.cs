using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Middlewares;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Tests.Contract.Fixtures;
using System.Text.Json;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Ops;

/// <summary>
/// Contract tests for request size limit enforcement on chat endpoints.
/// Verifies that MaxRequestBytes is enforced at the server layer.
/// </summary>
public sealed class RequestSizeLimitContractTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RequestSizeLimitContractTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RequestSizeLimitMiddleware_OversizeRequest_Returns413WithErrorEnvelope()
    {
        // Arrange - This is the integration test that verifies end-to-end middleware ordering
        var client = _factory.CreateClient();
        
        // Create oversized request body (> 1MB default limit)
        var largePayload = new string('x', 2 * 1024 * 1024); // 2MB
        var requestBody = new
        {
            model = "test-model",
            messages = new[] { new { role = "user", content = largePayload } }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), 
            System.Text.Encoding.UTF8, 
            "application/json");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test", "test-token");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.RequestEntityTooLarge, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), "Response body should not be empty");

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Verify ErrorEnvelope structure
        Assert.True(root.TryGetProperty("success", out var success), "Missing 'success' property");
        Assert.False(success.GetBoolean(), "success should be false");

        Assert.True(root.TryGetProperty("error", out var error), "Missing 'error' property");
        Assert.True(error.TryGetProperty("code", out var code), "Missing error.code");
        Assert.Equal("REQUEST_TOO_LARGE", code.GetString());

        Assert.True(error.TryGetProperty("messageKey", out _), "Missing error.messageKey");
        Assert.True(error.TryGetProperty("localizedMessage", out _), "Missing error.localizedMessage");
        
        // Verify observability fields
        Assert.True(error.TryGetProperty("correlationId", out _), "Missing error.correlationId");
        Assert.True(error.TryGetProperty("traceId", out _), "Missing error.traceId");
        Assert.True(error.TryGetProperty("requestId", out _), "Missing error.requestId");
        
        // Verify root-level observability fields
        Assert.True(root.TryGetProperty("correlationId", out _), "Missing root correlationId");
        Assert.True(root.TryGetProperty("traceId", out _), "Missing root traceId");
    }

    [Fact]
    public async Task RequestSizeLimitMiddleware_EnforcesLimit_OnChatEndpoint()
    {
        // Arrange
        var chatOptions = Options.Create(new ChatOptions
        {
            MaxRequestBytes = 1024 // 1KB limit for testing
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/chat";
        context.Request.ContentLength = 2048; // Exceeds limit

        var middleware = new RequestSizeLimitMiddleware(
            _ => Task.CompletedTask,
            chatOptions,
            new FakeLogger());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TilsoftApiException>(
            async () => await middleware.InvokeAsync(context));

        Assert.NotNull(exception);
        Assert.Equal(ErrorCode.RequestTooLarge, exception.Code);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, exception.HttpStatusCode);
    }

    [Fact]
    public async Task RequestSizeLimitMiddleware_AllowsRequest_BelowLimit()
    {
        // Arrange
        var chatOptions = Options.Create(new ChatOptions
        {
            MaxRequestBytes = 1024
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/chat";
        context.Request.ContentLength = 512; // Below limit

        var called = false;
        var middleware = new RequestSizeLimitMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            chatOptions,
            new FakeLogger());

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task RequestSizeLimitMiddleware_IgnoresNonChatEndpoints()
    {
        // Arrange
        var chatOptions = Options.Create(new ChatOptions
        {
            MaxRequestBytes = 1024
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/other"; // Not a chat endpoint
        context.Request.ContentLength = 2048; // Would exceed limit

        var called = false;
        var middleware = new RequestSizeLimitMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            chatOptions,
            new FakeLogger());

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Should pass through without error
        Assert.True(called);
    }

    [Theory]
    [InlineData("/api/chat")]
    [InlineData("/api/chat/stream")]
    [InlineData("/v1/chat/completions")]
    public async Task RequestSizeLimitMiddleware_EnforcesOnAllChatRoutes(string path)
    {
        // Arrange
        var chatOptions = Options.Create(new ChatOptions
        {
            MaxRequestBytes = 1024
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = path;
        context.Request.ContentLength = 2048; // Exceeds limit

        var middleware = new RequestSizeLimitMiddleware(
            _ => Task.CompletedTask,
            chatOptions,
            new FakeLogger());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TilsoftApiException>(
            async () => await middleware.InvokeAsync(context));

        Assert.NotNull(exception);
        Assert.Equal(ErrorCode.RequestTooLarge, exception.Code);
    }

    [Fact]
    public async Task RequestSizeLimitMiddleware_EnforcesChunkedOversize()
    {
        // Arrange
        var chatOptions = Options.Create(new ChatOptions
        {
            MaxRequestBytes = 1024 // 1KB limit
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/chat";
        // No Content-Length set to simulate chunked transfer
        context.Request.Body = new MemoryStream(new byte[2048]); // 2KB body

        var middleware = new RequestSizeLimitMiddleware(
            _ => Task.CompletedTask,
            chatOptions,
            new FakeLogger());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TilsoftApiException>(
            async () => await middleware.InvokeAsync(context));

        Assert.Equal(ErrorCode.RequestTooLarge, exception.Code);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, exception.HttpStatusCode);
    }

    [Fact]
    public async Task RequestSizeLimitMiddleware_AllowsChunkedWithinLimit()
    {
        // Arrange
        var chatOptions = Options.Create(new ChatOptions
        {
            MaxRequestBytes = 2048 // 2KB limit
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/chat";
        // No Content-Length set to simulate chunked transfer
        context.Request.Body = new MemoryStream(new byte[1024]); // 1KB body, within limit

        var called = false;
        var middleware = new RequestSizeLimitMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            chatOptions,
            new FakeLogger());

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Should pass through without error
        Assert.True(called);
        Assert.Equal(0, context.Request.Body.Position); // Stream reset to beginning
    }

    [Fact]
    public async Task RequestSizeLimitMiddleware_ChunkedDoesNotCauseOOM()
    {
        // Arrange - simulate very large request
        var chatOptions = Options.Create(new ChatOptions
        {
            MaxRequestBytes = 1024 // 1KB limit
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/chat";
        // Simulate 10MB body that should be rejected early
        context.Request.Body = new MemoryStream(new byte[10 * 1024 * 1024]);

        var middleware = new RequestSizeLimitMiddleware(
            _ => Task.CompletedTask,
            chatOptions,
            new FakeLogger());

        // Act & Assert - Should reject early without reading entire 10MB
        var exception = await Assert.ThrowsAsync<TilsoftApiException>(
            async () => await middleware.InvokeAsync(context));

        Assert.Equal(ErrorCode.RequestTooLarge, exception.Code);
        // Should abort soon after exceeding limit, not after reading all 10MB
        Assert.True(context.Request.Body.Position < 10 * 1024 * 1024);
    }

    private sealed class FakeLogger : ILogger<RequestSizeLimitMiddleware>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
