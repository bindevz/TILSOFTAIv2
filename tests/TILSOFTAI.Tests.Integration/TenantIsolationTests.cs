using System.Net;
using System.Net.Http.Json;
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

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task TenantIsolation_ClaimHeaderMismatch_IsRejected()
    {
        await using var factory = new TenantIsolationFactory(includeClaims: true, allowHeaderFallback: false);
        var client = factory.CreateClient();

        using var request = CreateChatRequest();
        request.Headers.Add("X-Tenant-Id", "tenant-2");
        request.Headers.Add("X-User-Id", "user-1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TenantIsolation_HeaderFallbackDisabled_IsRejected()
    {
        await using var factory = new TenantIsolationFactory(includeClaims: false, allowHeaderFallback: false);
        var client = factory.CreateClient();

        using var request = CreateChatRequest();
        request.Headers.Add("X-Tenant-Id", "tenant-1");
        request.Headers.Add("X-User-Id", "user-1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TenantIsolation_HeaderFallbackEnabled_IsAccepted()
    {
        await using var factory = new TenantIsolationFactory(includeClaims: false, allowHeaderFallback: true);
        var client = factory.CreateClient();

        using var request = CreateChatRequest();
        request.Headers.Add("X-Tenant-Id", "tenant-1");
        request.Headers.Add("X-User-Id", "user-1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage CreateChatRequest()
    {
        return new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new
            {
                input = "Hello",
                allowCache = true,
                containsSensitive = false
            })
        };
    }

    private sealed class TenantIsolationFactory : WebApplicationFactory<Program>
    {
        private readonly bool _includeClaims;
        private readonly bool _allowHeaderFallback;
        private readonly string _tenantClaim;
        private readonly string _userClaim;

        public TenantIsolationFactory(
            bool includeClaims,
            bool allowHeaderFallback,
            string tenantClaim = "tenant-1",
            string userClaim = "user-1")
        {
            _includeClaims = includeClaims;
            _allowHeaderFallback = allowHeaderFallback;
            _tenantClaim = tenantClaim;
            _userClaim = userClaim;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:AllowHeaderTenantFallback"] = _allowHeaderFallback ? "true" : "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<TestAuthOptions, TestAuthHandler>("Test", options =>
                    {
                        if (_includeClaims)
                        {
                            options.TenantId = _tenantClaim;
                            options.UserId = _userClaim;
                        }
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
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
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
            if (!string.IsNullOrWhiteSpace(Options.TenantId))
            {
                identity.AddClaim(new System.Security.Claims.Claim(TilsoftClaims.TenantId, Options.TenantId));
            }

            if (!string.IsNullOrWhiteSpace(Options.UserId))
            {
                identity.AddClaim(new System.Security.Claims.Claim(TilsoftClaims.UserId, Options.UserId));
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
