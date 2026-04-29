using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TILSOFTAI.Api.Auth;
using TILSOFTAI.Api.Options;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Security;

namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiAuthExtensions
{
    public static IServiceCollection AddTilsoftAiAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<JwtSigningKeyProvider>();
        services.AddSingleton<IJwtSigningKeyProvider>(sp => sp.GetRequiredService<JwtSigningKeyProvider>());
        services.AddHostedService<JwtSigningKeyRefreshHostedService>();

        var authEnabled = configuration.GetValue<bool>("Auth:Enabled", true);
        if (authEnabled)
        {
            ConfigureAuthentication(services);

            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
        }
        else
        {
            services.AddAuthentication("NoAuth")
                .AddScheme<AuthenticationSchemeOptions, NoAuthHandler>("NoAuth", _ => { });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder("NoAuth")
                    .RequireAssertion(_ => true)
                    .Build();
                options.FallbackPolicy = null;
            });
        }

        return services;
    }

    private static void ConfigureAuthentication(IServiceCollection services)
    {
        services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IJwtSigningKeyProvider, ILoggerFactory, IAuditLogger, IOptions<AuthOptions>>((jwtOptions, keyProvider, loggerFactory, auditLogger, authOptions) =>
            {
                jwtOptions.TokenValidationParameters ??= new TokenValidationParameters();
                jwtOptions.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, _, _) =>
                {
                    var keys = keyProvider.GetKeys();
                    if (keys.Count == 0)
                    {
                        var logger = loggerFactory.CreateLogger("JwtAuthentication");
                        logger.LogWarning("JWT signing key resolver returned empty key set. Token validation will fail.");
                    }
                    return keys;
                };

                jwtOptions.Events ??= new JwtBearerEvents();

                jwtOptions.Events.OnAuthenticationFailed = context =>
                {
                    var logger = loggerFactory.CreateLogger("JwtAuthentication");
                    var correlationId = context.HttpContext.TraceIdentifier;

                    logger.LogWarning(
                        context.Exception,
                        "JWT authentication failed. CorrelationId: {CorrelationId}, Failure: {FailureMessage}",
                        correlationId,
                        context.Exception?.Message ?? "Unknown");

                    var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                    var userAgent = context.HttpContext.Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;
                    auditLogger.LogAuthenticationEvent(AuthAuditEvent.Failure(
                        correlationId,
                        ipAddress,
                        userAgent.Length > 500 ? userAgent[..500] : userAgent,
                        context.Exception?.Message ?? "Unknown"));

                    return Task.CompletedTask;
                };

                jwtOptions.Events.OnTokenValidated = context =>
                {
                    var correlationId = context.HttpContext.TraceIdentifier;
                    var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                    var userAgent = context.HttpContext.Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;

                    var tenantClaim = context.Principal?.FindFirst(authOptions.Value.TenantClaimName)?.Value ?? string.Empty;
                    var userIdClaim = context.Principal?.FindFirst(authOptions.Value.UserIdClaimName)?.Value ?? string.Empty;

                    var claims = new Dictionary<string, string>();
                    if (context.Principal?.Claims != null)
                    {
                        foreach (var claim in context.Principal.Claims.Take(20))
                        {
                            claims[claim.Type] = claim.Value.Length > 100 ? claim.Value[..100] + "..." : claim.Value;
                        }
                    }

                    auditLogger.LogAuthenticationEvent(AuthAuditEvent.Success(
                        tenantClaim,
                        userIdClaim,
                        correlationId,
                        ipAddress,
                        userAgent.Length > 500 ? userAgent[..500] : userAgent,
                        claims));

                    return Task.CompletedTask;
                };
            });
    }
}
