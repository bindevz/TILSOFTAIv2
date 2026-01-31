using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TILSOFTAI.Api.Auth;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Options;

/// <summary>
/// Configures JwtBearerOptions using validated IOptions&lt;AuthOptions&gt;.
/// </summary>
public sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IOptions<AuthOptions> _authOptions;

    public ConfigureJwtBearerOptions(IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        // Only configure the JwtBearer scheme
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var authOpts = _authOptions.Value;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOpts.Issuer,
            ValidateAudience = true,
            ValidAudience = authOpts.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(authOpts.ClockSkewSeconds),
            RoleClaimType = authOpts.RoleClaimName
        };
    }

    public void Configure(JwtBearerOptions options)
    {
        // This will be called for unnamed options, but we handle named options
        Configure(Microsoft.Extensions.Options.Options.DefaultName, options);
    }
}
