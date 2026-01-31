using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Middlewares;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Ops;

/// <summary>
/// Contract tests for request size limit enforcement on chat endpoints.
/// Verifies that MaxRequestBytes is enforced at the server layer.
/// </summary>
public sealed class RequestSizeLimitContractTests
{
    [Fact]
    public void RequestSizeLimitMiddleware_EnforcesLimit_OnChatEndpoint()
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
        var exception = Assert.ThrowsAsync<TilsoftApiException>(
            async () => await middleware.InvokeAsync(context));

        Assert.NotNull(exception);
        Assert.Equal(ErrorCode.RequestTooLarge, exception.Result.Code);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, exception.Result.HttpStatusCode);
    }

    [Fact]
    public void RequestSizeLimitMiddleware_AllowsRequest_BelowLimit()
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
        middleware.InvokeAsync(context).Wait();

        // Assert
        Assert.True(called);
    }

    [Fact]
    public void RequestSizeLimitMiddleware_IgnoresNonChatEndpoints()
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
        middleware.InvokeAsync(context).Wait();

        // Assert - Should pass through without error
        Assert.True(called);
    }

    [Theory]
    [InlineData("/api/chat")]
    [InlineData("/api/chat/stream")]
    [InlineData("/v1/chat/completions")]
    public void RequestSizeLimitMiddleware_EnforcesOnAllChatRoutes(string path)
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
        var exception = Assert.ThrowsAsync<TilsoftApiException>(
            async () => await middleware.InvokeAsync(context));

        Assert.NotNull(exception);
        Assert.Equal(ErrorCode.RequestTooLarge, exception.Result.Code);
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
