using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Auth;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Normalization;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Tools;
using Xunit;

namespace TILSOFTAI.Tests.Integration;

public sealed class ErrorDisclosureTests
{
    [Fact]
    public async Task ErrorDetail_IsHidden_WhenExposeErrorDetailDisabled()
    {
        await using var factory = new ErrorDisclosureFactory(
            environmentName: "Production",
            exposeErrorDetail: false,
            roles: Array.Empty<string>());
        var client = factory.CreateClient();

        using var request = CreateChatRequest(string.Empty);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        var error = doc.RootElement.GetProperty("error");

        Assert.False(error.TryGetProperty("detail", out var detail) && detail.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task ErrorDetail_IsExposed_ForAdminRole()
    {
        await using var factory = new ErrorDisclosureFactory(
            environmentName: "Production",
            exposeErrorDetail: true,
            roles: new[] { "ai_admin" });
        var client = factory.CreateClient();

        using var request = CreateChatRequest(string.Empty);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        var error = doc.RootElement.GetProperty("error");

        Assert.True(error.TryGetProperty("detail", out var detail)
            && detail.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(detail.GetString()));
    }

    [Fact]
    public async Task ChatStream_ErrorEvent_UsesEnvelope()
    {
        await using var factory = new ErrorDisclosureFactory(
            environmentName: "Production",
            exposeErrorDetail: false,
            roles: Array.Empty<string>());
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream")
        {
            Content = JsonContent.Create(new
            {
                input = string.Empty,
                allowCache = false,
                containsSensitive = false
            })
        };

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        string currentEvent = string.Empty;
        string? errorData = null;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                currentEvent = line[7..].Trim();
                continue;
            }

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                if (string.Equals(currentEvent, "error", StringComparison.OrdinalIgnoreCase))
                {
                    errorData = line[6..];
                    break;
                }
            }
        }

        Assert.False(string.IsNullOrWhiteSpace(errorData));

        using var doc = JsonDocument.Parse(errorData!);
        var root = doc.RootElement;

        Assert.Equal("error", root.GetProperty("type").GetString());
        var payload = root.GetProperty("payload");
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("messageKey").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("localizedMessage").GetString()));
        Assert.False(payload.TryGetProperty("detail", out var detail) && detail.ValueKind != JsonValueKind.Null);
    }

    private static HttpRequestMessage CreateChatRequest(string input)
    {
        return new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new
            {
                input,
                allowCache = true,
                containsSensitive = false
            })
        };
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(payload);
    }

    private sealed class ErrorDisclosureFactory : WebApplicationFactory<Program>
    {
        private readonly string _environmentName;
        private readonly bool _exposeErrorDetail;
        private readonly string[] _roles;

        public ErrorDisclosureFactory(string environmentName, bool exposeErrorDetail, string[] roles)
        {
            _environmentName = environmentName;
            _exposeErrorDetail = exposeErrorDetail;
            _roles = roles;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environmentName);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ErrorHandling:ExposeErrorDetail"] = _exposeErrorDetail ? "true" : "false",
                    ["ErrorHandling:ExposeErrorDetailRoles:0"] = "ai_admin",
                    ["ErrorHandling:MaxDetailLength"] = "1024"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<TestAuthOptions, TestAuthHandler>("Test", options =>
                    {
                        options.Roles = _roles;
                    });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                });

                services.RemoveAll<ILlmClient>();
                services.AddSingleton<ILlmClient, FakeLlmClient>();

                services.RemoveAll<IToolCatalogResolver>();
                services.AddSingleton<IToolCatalogResolver, FakeToolCatalogResolver>();

                services.RemoveAll<INormalizationRuleProvider>();
                services.AddSingleton<INormalizationRuleProvider, FakeNormalizationRuleProvider>();

                services.RemoveAll<IContextPackProvider>();
                services.AddSingleton<IContextPackProvider, FakeContextPackProvider>();
            });
        }
    }

    private sealed class TestAuthOptions : AuthenticationSchemeOptions
    {
        public string[] Roles { get; set; } = Array.Empty<string>();
    }

    private sealed class TestAuthHandler : AuthenticationHandler<TestAuthOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<TestAuthOptions> options,
            ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new System.Security.Claims.ClaimsIdentity("Test");
            identity.AddClaim(new System.Security.Claims.Claim(TilsoftClaims.TenantId, "tenant-1"));
            identity.AddClaim(new System.Security.Claims.Claim(TilsoftClaims.UserId, "user-1"));

            foreach (var role in Options.Roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    identity.AddClaim(new System.Security.Claims.Claim(TilsoftClaims.Roles, role));
                }
            }

            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class FakeLlmClient : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(LlmRequest req, CancellationToken ct)
        {
            return Task.FromResult(new LlmResponse { Content = "fake response" });
        }

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest req, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            yield return LlmStreamEvent.Final("ok");
            await Task.CompletedTask;
        }
    }

    private sealed class FakeToolCatalogResolver : IToolCatalogResolver
    {
        public Task<IReadOnlyList<ToolDefinition>> GetResolvedToolsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ToolDefinition>>(Array.Empty<ToolDefinition>());
        }
    }

    private sealed class FakeNormalizationRuleProvider : INormalizationRuleProvider
    {
        public Task<IReadOnlyList<NormalizationRuleRecord>> GetRulesAsync(string tenantId, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<NormalizationRuleRecord>>(Array.Empty<NormalizationRuleRecord>());
        }
    }

    private sealed class FakeContextPackProvider : IContextPackProvider
    {
        public Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(TilsoftExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }
    }
}
