using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Middlewares;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Security;
using TILSOFTAI.Infrastructure.Errors;
using TILSOFTAI.Infrastructure.Observability;
using TILSOFTAI.Domain.Metrics;
using Moq;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Errors;

/// <summary>
/// Contract tests verifying that validation errors (TOOL_ARGS_INVALID, INVALID_ARGUMENT)
/// always return safe, structured paths regardless of error detail policy.
/// </summary>
public sealed class ToolArgsInvalidDetailContractTests
{
    [Fact]
    public async Task ExceptionHandlingMiddleware_ReturnsValidationPaths_RegardlessOfDetailPolicy()
    {
        // Arrange - Production mode with all detail exposure disabled
        var identityPolicy = new IdentityResolutionPolicy(Options.Create(new LocalizationOptions
        {
            DefaultLanguage = "en"
        }));

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new TilsoftApiException(
                ErrorCode.InvalidArgument,
                StatusCodes.Status400BadRequest,
                detail: new[] { "/input: too long", "/metadata/key: required" }),
            new InMemoryErrorCatalog(Options.Create(new LocalizationOptions { DefaultLanguage = "en" })),
            new NullSqlErrorLogWriter(),
            Options.Create(new AuthOptions
            {
                TenantClaimName = "tid",
                UserIdClaimName = "sub",
                TrustedGatewayClaimName = "gateway"
            }),
            Options.Create(new ObservabilityOptions { EnableSqlErrorLog = false }),
            Options.Create(new ErrorHandlingOptions
            {
                ExposeErrorDetail = false,
                ExposeErrorDetailInDevelopment = false,
                ExposeErrorDetailRoles = Array.Empty<string>(),
                MaxDetailLength = 1024
            }),
            identityPolicy,
            new ProductionEnvironment(),
            new BasicLogRedactor(),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            Mock.Of<IMetricsService>());

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.User = BuildRegularUser();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        var detail = doc.RootElement.GetProperty("error").GetProperty("detail");

        Assert.Equal(JsonValueKind.Array, detail.ValueKind);
        Assert.Equal(2, detail.GetArrayLength());
        
        Assert.Equal("/input", detail[0].GetProperty("path").GetString());
        Assert.Equal(ErrorCode.InvalidArgument, detail[0].GetProperty("messageKey").GetString());
        
        Assert.Equal("/metadata/key", detail[1].GetProperty("path").GetString());
        Assert.Equal(ErrorCode.InvalidArgument, detail[1].GetProperty("messageKey").GetString());
    }

    [Fact]
    public void ChatStreamEnvelopeFactory_ReturnsValidationPaths_RegardlessOfDetailPolicy()
    {
        // Arrange - Production mode
        var factory = new ChatStreamEnvelopeFactory(
            new InMemoryErrorCatalog(Options.Create(new LocalizationOptions { DefaultLanguage = "en" })),
            Options.Create(new ErrorHandlingOptions
            {
                ExposeErrorDetail = false,
                ExposeErrorDetailInDevelopment = false,
                ExposeErrorDetailRoles = Array.Empty<string>(),
                MaxDetailLength = 1024
            }),
            new ProductionEnvironment(),
            new BasicLogRedactor());

        var errorEnvelope = new ErrorEnvelope
        {
            Code = ErrorCode.ToolArgsInvalid,
            Detail = new[] { "/args/value: invalid" }
        };

        var streamEvent = new TILSOFTAI.Orchestration.Pipeline.ChatStreamEvent("error", errorEnvelope);

        var context = new TilsoftExecutionContext
        {
            Language = "en",
            Roles = Array.Empty<string>(), // Regular user
            CorrelationId = "test-correlation",
            TraceId = "test-trace",
            ConversationId = "test-conversation"
        };

        // Act
        var envelope = factory.Create(streamEvent, context);

        // Assert
        Assert.NotNull(envelope.Payload);
        var error = envelope.Payload as ErrorEnvelope;
        Assert.NotNull(error);
        Assert.NotNull(error!.Detail);

        var detailJson = JsonSerializer.Serialize(error.Detail);
        using var doc = JsonDocument.Parse(detailJson);
        var detailArray = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, detailArray.ValueKind);
        Assert.Equal("/args/value", detailArray[0].GetProperty("path").GetString());
        Assert.Equal(ErrorCode.ToolArgsInvalid, detailArray[0].GetProperty("messageKey").GetString());
    }

    [Fact]
    public async Task NonValidationErrors_RespectDetailPolicy()
    {
        // Arrange
        var identityPolicy = new IdentityResolutionPolicy(Options.Create(new LocalizationOptions
        {
            DefaultLanguage = "en"
        }));

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new TilsoftApiException(
                ErrorCode.ChatFailed,
                StatusCodes.Status400BadRequest,
                detail: new { sensitive = "data" }),
            new InMemoryErrorCatalog(Options.Create(new LocalizationOptions { DefaultLanguage = "en" })),
            new NullSqlErrorLogWriter(),
            Options.Create(new AuthOptions
            {
                TenantClaimName = "tid",
                UserIdClaimName = "sub",
                TrustedGatewayClaimName = "gateway"
            }),
            Options.Create(new ObservabilityOptions { EnableSqlErrorLog = false }),
            Options.Create(new ErrorHandlingOptions
            {
                ExposeErrorDetail = false,
                ExposeErrorDetailInDevelopment = false,
                ExposeErrorDetailRoles = Array.Empty<string>(),
                MaxDetailLength = 1024
            }),
            identityPolicy,
            new ProductionEnvironment(),
            new BasicLogRedactor(),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            Mock.Of<IMetricsService>());

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.User = BuildRegularUser();

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Detail should be null for non-validation errors when policy disables it
        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        var error = doc.RootElement.GetProperty("error");
        
        // Detail should not be present or be null
        if (error.TryGetProperty("detail", out var detail))
        {
            Assert.Equal(JsonValueKind.Null, detail.ValueKind);
        }
    }

    private static ClaimsPrincipal BuildRegularUser()
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("tid", "tenant-1"));
        identity.AddClaim(new Claim("sub", "user-1"));
        return new ClaimsPrincipal(identity);
    }

    private sealed class NullSqlErrorLogWriter : ISqlErrorLogWriter
    {
        public Task WriteAsync(TilsoftExecutionContext context, string code, string message, object? detail, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class ProductionEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "tests";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
