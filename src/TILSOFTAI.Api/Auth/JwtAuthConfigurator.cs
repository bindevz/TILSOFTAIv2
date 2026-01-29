using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
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

        return services
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

                jwtOptions.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, _, _) =>
                {
                    if (string.IsNullOrWhiteSpace(options.JwksUrl))
                    {
                        return Array.Empty<SecurityKey>();
                    }

                    var jwks = JwksCache.GetAsync(options.JwksUrl).GetAwaiter().GetResult();
                    return jwks?.Keys?.Cast<SecurityKey>() ?? Array.Empty<SecurityKey>();
                };
            });
    }

    private static class JwksCache
    {
        private static readonly ConcurrentDictionary<string, (DateTimeOffset fetchedAt, JsonWebKeySet jwks)> Cache = new();
        private static readonly HttpClient HttpClient = new();
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

        public static async Task<JsonWebKeySet?> GetAsync(string jwksUrl)
        {
            if (Cache.TryGetValue(jwksUrl, out var entry) && DateTimeOffset.UtcNow - entry.fetchedAt < Ttl)
            {
                return entry.jwks;
            }

            var jwks = await HttpClient.GetFromJsonAsync<JsonWebKeySet>(jwksUrl);
            if (jwks is not null)
            {
                Cache[jwksUrl] = (DateTimeOffset.UtcNow, jwks);
            }

            return jwks;
        }
    }
}
