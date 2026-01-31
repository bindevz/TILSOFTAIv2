using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Domain.Security;

/// <summary>
/// Resolves identity for SignalR hub invocations using claims-first approach.
/// No header fallback for SignalR - JWT claims only.
/// </summary>
public sealed class HubIdentityResolutionPolicy
{
    private readonly LocalizationOptions _localizationOptions;

    public HubIdentityResolutionPolicy(IOptions<LocalizationOptions> localizationOptions)
    {
        _localizationOptions = localizationOptions?.Value ?? new LocalizationOptions();
    }

    /// <summary>
    /// Resolves identity for a SignalR hub invocation.
    /// Uses JWT claims only - no header fallback.
    /// Supports language negotiation via querystring 'lang' parameter.
    /// </summary>
    public IdentityResolutionResult ResolveForHub(
        HubCallerContext hubContext,
        AuthOptions authOptions)
    {
        if (hubContext is null)
        {
            throw new ArgumentNullException(nameof(hubContext));
        }
        if (authOptions is null)
        {
            throw new ArgumentNullException(nameof(authOptions));
        }

        var principal = hubContext.User;
        var correlationId = ResolveCorrelationId();
        var conversationId = correlationId; // Use correlationId as conversationId for SignalR
        var traceId = ResolveTraceId(correlationId);
        var requestId = hubContext.ConnectionId;

        // Resolve from claims only - no header fallback for SignalR
        var tenantId = GetClaim(principal, authOptions.TenantClaimName);
        var userId = GetClaim(principal, authOptions.UserIdClaimName);
        var roles = ResolveRoles(principal, authOptions);

        // Negotiate language from querystring or use default
        var language = NegotiateLanguage(hubContext);

        return new IdentityResolutionResult
        {
            TenantId = NormalizeIdentity(tenantId),
            UserId = NormalizeIdentity(userId),
            Roles = roles,
            Language = language,
            CorrelationId = correlationId,
            ConversationId = conversationId,
            TraceId = traceId,
            RequestId = requestId,
            IsHeaderSpoofAttempt = false // SignalR doesn't use headers, so no spoof risk
        };
    }

    private static string ResolveCorrelationId()
    {
        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString() : traceId;
    }

    private static string ResolveTraceId(string correlationId)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(traceId) ? correlationId : traceId;
    }

    private string ResolveDefaultLanguage()
    {
        var language = _localizationOptions.DefaultLanguage;
        return string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
    }

    /// <summary>
    /// Negotiates language from querystring 'lang' parameter.
    /// Validates against SupportedLanguages and falls back to default if invalid.
    /// </summary>
    private string NegotiateLanguage(HubCallerContext hubContext)
    {
        var httpContext = hubContext.GetHttpContext();
        if (httpContext is null)
        {
            return ResolveDefaultLanguage();
        }

        var langParam = httpContext.Request.Query["lang"].ToString();
        if (string.IsNullOrWhiteSpace(langParam))
        {
            return ResolveDefaultLanguage();
        }

        var normalized = langParam.Trim().ToLowerInvariant();

        // Validate against supported languages
        if (_localizationOptions.SupportedLanguages != null &&
            _localizationOptions.SupportedLanguages.Length > 0)
        {
            var isSupported = _localizationOptions.SupportedLanguages
                .Any(lang => string.Equals(lang, normalized, StringComparison.OrdinalIgnoreCase));

            if (isSupported)
            {
                return normalized;
            }
        }

        // Fallback to default if not supported
        return ResolveDefaultLanguage();
    }

    private static string? GetClaim(ClaimsPrincipal? principal, string claimType)
    {
        if (principal is null || string.IsNullOrWhiteSpace(claimType))
        {
            return null;
        }

        return principal.FindFirst(claimType)?.Value?.Trim();
    }

    private static string? NormalizeIdentity(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] ResolveRoles(ClaimsPrincipal? principal, AuthOptions authOptions)
    {
        if (principal is null || string.IsNullOrWhiteSpace(authOptions.RoleClaimName))
        {
            return Array.Empty<string>();
        }

        var roleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in principal.FindAll(authOptions.RoleClaimName))
        {
            foreach (var role in SplitRoles(claim.Value))
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    roleSet.Add(role.Trim());
                }
            }
        }

        return roleSet.ToArray();
    }

    private static IEnumerable<string> SplitRoles(string? roles)
    {
        return string.IsNullOrWhiteSpace(roles)
            ? Array.Empty<string>()
            : roles.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
