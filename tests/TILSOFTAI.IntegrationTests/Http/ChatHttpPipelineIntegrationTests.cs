using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Api.Controllers;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Sensitivity;
using TILSOFTAI.Infrastructure.Errors;
using TILSOFTAI.Infrastructure.ExecutionContext;
using TILSOFTAI.Infrastructure.Observability;
using TILSOFTAI.Infrastructure.Sensitivity;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Supervisor;
using Xunit;

namespace TILSOFTAI.IntegrationTests.Http;

public sealed class ChatHttpPipelineIntegrationTests
{
    [Fact]
    public async Task AuthenticatedChatPost_ShouldExecuteThroughAspNetPipeline()
    {
        await using var app = await CreateAppAsync();
        using var client = CreateClient(app);
        client.DefaultRequestHeaders.Authorization = new("Test");

        var response = await client.PostAsJsonAsync("/api/chats", new ChatApiRequest
        {
            Input = "show warehouse inventory summary"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ChatApiResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Content.Should().Be("http-ok");
        body.CorrelationId.Should().Be("corr-http");
    }

    [Fact]
    public async Task ChatController_RawJson_ReturnsDetailPayload()
    {
        await using var app = await CreateAppAsync();
        using var client = CreateClient(app);
        client.DefaultRequestHeaders.Authorization = new("Test");

        var response = await client.PostAsJsonAsync("/api/chats", new ChatApiRequest
        {
            Input = "raw json model overview"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ChatApiResponse>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Content.Should().BeEmpty();
        body.AnswerType.Should().Be("raw_json");
        body.Detail.Should().BeOfType<JsonElement>();
        var detail = (JsonElement)body.Detail!;
        detail.GetProperty("capabilityKey").GetString().Should().Be("model.overview.by-code");
        detail.GetProperty("procedureName").GetString().Should().Be("dbo.ai_model_get_overview");
        detail.GetProperty("arguments").GetProperty("modelCode").GetString().Should().Be("ABC");
        detail.GetProperty("rowCount").GetInt32().Should().Be(1);
        detail.GetProperty("resultSchema").ValueKind.Should().Be(JsonValueKind.Object);
        detail.GetProperty("executionMetadata").GetProperty("correlationId").GetString().Should().Be("corr-http");
        detail.GetProperty("provenance").GetProperty("correlationId").GetString().Should().Be("corr-http");
    }

    [Fact]
    public async Task ChatController_RawJson_DoesNotDropRows()
    {
        await using var app = await CreateAppAsync();
        using var client = CreateClient(app);
        client.DefaultRequestHeaders.Authorization = new("Test");

        var response = await client.PostAsJsonAsync("/api/chats", new ChatApiRequest
        {
            Input = "raw json model overview"
        });

        var body = await response.Content.ReadFromJsonAsync<ChatApiResponse>();
        var detail = (JsonElement)body!.Detail!;
        var rows = detail.GetProperty("rows");

        rows.GetArrayLength().Should().Be(1);
        rows[0].GetProperty("ModelCode").GetString().Should().Be("ABC");
        rows[0].GetProperty("ModelName").GetString().Should().Be("Chair");
    }

    [Fact]
    public async Task ChatController_RawJson_PreservesCorrelationId()
    {
        await using var app = await CreateAppAsync();
        using var client = CreateClient(app);
        client.DefaultRequestHeaders.Authorization = new("Test");

        var response = await client.PostAsJsonAsync("/api/chats", new ChatApiRequest
        {
            Input = "raw json model overview"
        });

        var body = await response.Content.ReadFromJsonAsync<ChatApiResponse>();

        body!.CorrelationId.Should().Be("corr-http");
        body.Provenance.Should().NotBeNull();
        body.Provenance!["correlationId"].Should().BeOfType<JsonElement>()
            .Which.GetString().Should().Be("corr-http");
    }

    [Fact]
    public async Task OpenAiCompatible_RawJson_HasSerializableContent()
    {
        await using var app = await CreateAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = "test-model",
            messages = new[]
            {
                new { role = "user", content = "raw json model overview" }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var content = payload.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        content.Should().NotBeNullOrWhiteSpace();
        using var rawJson = JsonDocument.Parse(content!);
        rawJson.RootElement.GetProperty("mode").GetString().Should().Be("raw_json");
        rawJson.RootElement.GetProperty("rows").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task AnonymousChatPost_ShouldFailAuthorizationBeforeRuntime()
    {
        await using var app = await CreateAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync("/api/chats", new ChatApiRequest
        {
            Input = "show warehouse inventory summary"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");

        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();

        builder.Services.AddSingleton<ExecutionContextAccessor>();
        builder.Services.AddSingleton<IExecutionContextAccessor>(sp => sp.GetRequiredService<ExecutionContextAccessor>());
        builder.Services.AddSingleton<ISupervisorRuntime, StubSupervisorRuntime>();
        builder.Services.AddSingleton<ISensitivityClassifier, BasicSensitivityClassifier>();
        builder.Services.AddSingleton<IErrorCatalog, InMemoryErrorCatalog>();
        builder.Services.AddSingleton<TILSOFTAI.Orchestration.Observability.ILogRedactor, BasicLogRedactor>();
        builder.Services.AddSingleton<ChatStreamEnvelopeFactory>();
        builder.Services.Configure<ChatOptions>(options => options.MaxInputChars = 4000);
        builder.Services.Configure<StreamingOptions>(_ => { });
        builder.Services.Configure<SensitiveDataOptions>(_ => { });
        builder.Services.Configure<ErrorHandlingOptions>(options =>
        {
            options.ExposeErrorDetailInDevelopment = true;
            options.MaxDetailLength = 2000;
        });

        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(ChatController).Assembly);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            var accessor = context.RequestServices.GetRequiredService<ExecutionContextAccessor>();
            accessor.Set(new TilsoftExecutionContext
            {
                TenantId = "tenant-http",
                UserId = "user-http",
                Roles = new[] { "ai_user" },
                CorrelationId = "corr-http",
                ConversationId = "conv-http",
                TraceId = "trace-http",
                Language = "en"
            });
            await next(context);
        });
        app.MapControllers().RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.Single()
            ?? throw new InvalidOperationException("Test server did not expose an address.");

        return new HttpClient
        {
            BaseAddress = new Uri(address)
        };
    }

    private sealed class StubSupervisorRuntime : ISupervisorRuntime
    {
        public Task<SupervisorResult> RunAsync(
            SupervisorRequest request,
            TilsoftExecutionContext ctx,
            CancellationToken ct)
        {
            if (request.Input.Contains("raw json", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(SupervisorResult.FromAssistantAnswer(CreateRawJsonAnswer(ctx)));
            }

            return Task.FromResult(SupervisorResult.Ok("http-ok", "warehouse"));
        }

        public async IAsyncEnumerable<SupervisorStreamEvent> RunStreamAsync(
            SupervisorRequest request,
            TilsoftExecutionContext ctx,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            yield return SupervisorStreamEvent.Final("http-ok");
            await Task.CompletedTask;
        }

        private static AssistantAnswer CreateRawJsonAnswer(TilsoftExecutionContext ctx)
        {
            var composer = new RawJsonAnswerComposer();
            var answer = composer.ComposeAsync(
                new AnswerComposerRequest
                {
                    Mode = AnswerMode.RawJson,
                    CapabilityKey = "model.overview.by-code",
                    ProcedureName = "dbo.ai_model_get_overview",
                    Arguments = new Dictionary<string, object?> { ["modelCode"] = "ABC" },
                    ResultSchema = new ResultSchema
                    {
                        Columns =
                        [
                            new ResultColumn { Name = "ModelCode", Label = "Model Code", Type = "string" },
                            new ResultColumn { Name = "ModelName", Label = "Model Name", Type = "string" }
                        ]
                    },
                    Result = null,
                    Rows =
                    [
                        new Dictionary<string, object?>
                        {
                            ["ModelCode"] = "ABC",
                            ["ModelName"] = "Chair"
                        }
                    ],
                    RowCount = 1,
                    ExecutionMetadata = new ExecutionMetadata
                    {
                        AdapterType = "sql",
                        Operation = "execute_query",
                        CorrelationId = ctx.CorrelationId
                    },
                    SensitivityPolicy = SensitivityPolicy.Default,
                    Locale = "en-US",
                    AnswerPolicy = AnswerPolicy.Default
                },
                CancellationToken.None).GetAwaiter().GetResult();

            return answer;
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "user-http"),
                    new Claim(ClaimTypes.Role, "ai_user")
                },
                Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
