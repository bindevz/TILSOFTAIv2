using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace TILSOFTAI.Tests.Contract.Fixtures;

/// <summary>
/// WebApplicationFactory for contract tests with fake auth and in-memory dependencies.
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
                ["Chat:MaxRequestBytes"] = "262144" // 256KB
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Add test authentication scheme
            services.AddAuthentication("TestAuth")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", options => { });
        });
    }
}

/// <summary>
/// Test authentication handler that creates claims from static test data.
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
        var claims = new[]
        {
            new Claim("tid", "TEST_TENANT"),
            new Claim("sub", "TEST_USER"),
            new Claim("roles", "Admin,User")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestAuth");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
