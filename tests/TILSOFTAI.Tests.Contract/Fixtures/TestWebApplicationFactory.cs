using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.Caching;
using TILSOFTAI.Tests.Contract.Fixtures.Fakes;

namespace TILSOFTAI.Tests.Contract.Fixtures;

/// <summary>
/// WebApplicationFactory for contract tests with fake auth and in-memory dependencies.
/// Supports per-request and per-SignalR-connection claims via X-Test-* headers.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override with test-safe configuration
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sql:ConnectionString"] = "Server=.;Database=TestDb;Integrated Security=true;",
                ["Redis:Enabled"] = "false",
                ["Observability:EnableConversationPersistence"] = "false",
                ["Observability:EnableSqlToolLog"] = "false",
                ["Observability:EnableSqlErrorLog"] = "false",
                ["Auth:AllowHeaderFallback"] = "true",
                ["Auth:Issuer"] = "https://test-issuer.local",
                ["Auth:Audience"] = "test-audience",
                ["Auth:JwksUrl"] = "https://test-issuer.local/.well-known/jwks.json",
                ["Auth:TenantClaimName"] = "tid",
                ["Auth:UserIdClaimName"] = "sub",
                ["Auth:RoleClaimName"] = "roles",
                ["Auth:TrustedGatewayClaimName"] = "gateway",
                ["Cors:Enabled"] = "false",
                ["Llm:Provider"] = "Null",
                ["Chat:MaxRequestBytes"] = "262144", // 256KB
                ["SemanticCache:Mode"] = "Off",
                ["Localization:DefaultLanguage"] = "en",
                ["Localization:SupportedLanguages:0"] = "en",
                ["Localization:SupportedLanguages:1"] = "es",
                ["Localization:SupportedLanguages:2"] = "fr",
                ["Validation:MaxInputLength"] = "32000",
                ["Validation:MaxToolArgumentLength"] = "8000",
                ["Validation:EnablePromptInjectionDetection"] = "true",
                ["Validation:BlockOnPromptInjection"] = "false",
                ["Audit:Enabled"] = "false",
                ["Audit:SqlEnabled"] = "false",
                ["Audit:FileEnabled"] = "false"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace authentication scheme with TestAuth that reads X-Test-* headers
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddAuthentication("TestAuth")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", options => { });

            // Replace production dependencies with fakes
            services.RemoveAll<ISqlExecutor>();
            services.AddSingleton<ISqlExecutor, FakeSqlExecutor>();

            services.RemoveAll<IConversationStore>();
            services.AddSingleton<IConversationStore, FakeConversationStore>();

            services.RemoveAll<ISqlErrorLogWriter>();
            services.AddSingleton<ISqlErrorLogWriter, FakeSqlErrorLogWriter>();

            // Note: ISemanticCache is already disabled via SemanticCache:Mode=Off configuration
            // No need to replace as it's configured to be disabled
        });
    }
}

/// <summary>
/// Test authentication handler that creates claims from X-Test-* headers.
/// Supports per-request and per-SignalR-connection claims for robust test isolation.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
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
        // Read claims from X-Test-* headers (test-only)
        var tenantId = Request.Headers["X-Test-Tenant"].FirstOrDefault() ?? "TEST_TENANT";
        var userId = Request.Headers["X-Test-User"].FirstOrDefault() ?? "TEST_USER";
        var rolesHeader = Request.Headers["X-Test-Roles"].FirstOrDefault() ?? "Admin,User";
        var language = Request.Headers["X-Test-Lang"].FirstOrDefault() ?? "en";

        var claims = new List<Claim>
        {
            new Claim("tid", tenantId),
            new Claim("sub", userId),
            new Claim("lang", language)
        };

        // Split roles by comma
        var roles = rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var role in roles)
        {
            claims.Add(new Claim("roles", role));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestAuth");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
