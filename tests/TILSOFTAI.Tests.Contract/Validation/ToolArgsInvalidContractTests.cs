using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Middlewares;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.Security;
using TILSOFTAI.Infrastructure.Observability;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Tools;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Validation;

public sealed class ToolArgsInvalidContractTests
{
    [Fact]
    public void ToolSchemaInvalid_ReturnsToolArgsInvalid()
    {
        var schema = """
{
  "type": "object",
  "required": ["modelId"],
  "properties": {
    "modelId": { "type": "string" }
  },
  "additionalProperties": false
}
""";

        var tool = new ToolDefinition
        {
            Name = "demo_tool",
            JsonSchema = schema,
            SpName = "ai_demo_tool"
        };

        var call = new LlmToolCall
        {
            Name = "demo_tool",
            ArgumentsJson = "{}"
        };

        var context = new Domain.ExecutionContext.TilsoftExecutionContext
        {
            Language = "en",
            Roles = Array.Empty<string>()
        };

        var governance = new ToolGovernance(new BasicJsonSchemaValidator());
        var result = governance.Validate(call, new Dictionary<string, ToolDefinition>
        {
            ["demo_tool"] = tool
        }, context);

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCode.ToolArgsInvalid, result.Code);

        var errors = result.Detail as IEnumerable<string>;
        Assert.NotNull(errors);
        Assert.NotEmpty(errors!);
        Assert.Contains(errors!, error => error.StartsWith("/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Middleware_MapsToolArgsInvalid_DetailToStructuredPaths()
    {
        var identityPolicy = new IdentityResolutionPolicy(Options.Create(new LocalizationOptions
        {
            DefaultLanguage = "en"
        }));

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new TilsoftApiException(
                ErrorCode.ToolArgsInvalid,
                StatusCodes.Status400BadRequest,
                detail: new[] { "/modelId: required" }),
            new TILSOFTAI.Infrastructure.Errors.InMemoryErrorCatalog(Options.Create(new LocalizationOptions
            {
                DefaultLanguage = "en"
            })),
            new NullSqlErrorLogWriter(),
            Options.Create(new AuthOptions
            {
                TenantClaimName = "tid",
                UserIdClaimName = "sub",
                TrustedGatewayClaimName = "gateway_trusted"
            }),
            Options.Create(new ObservabilityOptions { EnableSqlErrorLog = false }),
            Options.Create(new ErrorHandlingOptions { ExposeErrorDetailInDevelopment = true }),
            identityPolicy,
            new TestEnvironment(),
            new BasicLogRedactor(),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.User = BuildPrincipal();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        var detail = doc.RootElement.GetProperty("error").GetProperty("detail");

        Assert.Equal(JsonValueKind.Array, detail.ValueKind);
        var first = detail[0];
        Assert.Equal("/modelId", first.GetProperty("path").GetString());
        Assert.Equal(ErrorCode.ToolArgsInvalid, first.GetProperty("messageKey").GetString());
    }

    private static ClaimsPrincipal BuildPrincipal()
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim("tid", "tenant-1"));
        identity.AddClaim(new Claim("sub", "user-1"));
        return new ClaimsPrincipal(identity);
    }

    private sealed class NullSqlErrorLogWriter : ISqlErrorLogWriter
    {
        public Task WriteAsync(Domain.ExecutionContext.TilsoftExecutionContext context, string code, string message, object? detail, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
