using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
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
            CancellationToken ct) => Task.FromResult(SupervisorResult.Ok("http-ok", "warehouse"));

        public async IAsyncEnumerable<SupervisorStreamEvent> RunStreamAsync(
            SupervisorRequest request,
            TilsoftExecutionContext ctx,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            yield return SupervisorStreamEvent.Final("http-ok");
            await Task.CompletedTask;
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
