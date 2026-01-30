using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Auth;

public static class JwtAuthConfigurator
{
    public static AuthenticationBuilder AddTilsoftJwtAuthentication(
        this IServiceCollection services,
        IOptions<AuthOptions> authOptions)
    {
        var options = authOptions.Value;

        var builder = services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(options.ClockSkewSeconds),
                    RoleClaimType = options.RoleClaimName
                };
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IJwtSigningKeyProvider, ILoggerFactory>((jwtOptions, keyProvider, loggerFactory) =>
            {
                jwtOptions.TokenValidationParameters ??= new TokenValidationParameters();
                jwtOptions.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, _, _) => keyProvider.GetKeys();

                jwtOptions.Events ??= new JwtBearerEvents();
                jwtOptions.Events.OnAuthenticationFailed = context =>
                {
                    var logger = loggerFactory.CreateLogger("JwtAuthentication");
                    logger.LogWarning(context.Exception, "JWT authentication failed.");
                    return Task.CompletedTask;
                };
            });

        return builder;
    }
}
