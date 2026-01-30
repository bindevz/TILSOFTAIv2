using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Domain.Security;

public sealed class IdentityResolutionPolicy
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string ConversationHeader = "X-Conversation-Id";
    private const string LanguageHeader = "X-Lang";
    private const string AcceptLanguageHeader = "Accept-Language";

    private readonly LocalizationOptions _localizationOptions;

    public IdentityResolutionPolicy(IOptions<LocalizationOptions> localizationOptions)
    {
        _localizationOptions = localizationOptions?.Value ?? new LocalizationOptions();
    }

    public IdentityResolutionResult ResolveForRequest(HttpContext context, AuthOptions authOptions, IWebHostEnvironment environment)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        if (authOptions is null)
        {
            throw new ArgumentNullException(nameof(authOptions));
        }
        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        var allowHeaderFallback = IsHeaderFallbackAllowed(context.User, authOptions, environment);
        return ResolveCore(context, authOptions, allowHeaderFallback);
    }

    public IdentityResolutionResult ResolveForError(HttpContext context, AuthOptions authOptions, IWebHostEnvironment environment)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        if (authOptions is null)
        {
            throw new ArgumentNullException(nameof(authOptions));
        }
        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        // Never trust header-based tenant/user for error handling.
        return ResolveCore(context, authOptions, allowHeaderFallback: false);
    }

    private IdentityResolutionResult ResolveCore(
        HttpContext context,
        AuthOptions authOptions,
        bool allowHeaderFallback)
    {
        var correlationId = ResolveCorrelationId(context);
        var conversationId = ResolveConversationId(context, correlationId);
        var traceId = ResolveTraceId(correlationId);
        var requestId = context.TraceIdentifier;
        var language = ResolveLanguage(context);

        var tenantClaim = GetClaim(context.User, authOptions.TenantClaimName);
        var userClaim = GetClaim(context.User, authOptions.UserIdClaimName);

        var headerTenant = GetFirstHeader(context, authOptions.HeaderTenantKeys);
        var headerUser = GetFirstHeader(context, authOptions.HeaderUserKeys);

        var isSpoofAttempt = IsMismatch(tenantClaim, headerTenant) || IsMismatch(userClaim, headerUser);

        var tenantId = !string.IsNullOrWhiteSpace(tenantClaim)
            ? tenantClaim
            : (allowHeaderFallback ? headerTenant : null);

        var userId = !string.IsNullOrWhiteSpace(userClaim)
            ? userClaim
            : (allowHeaderFallback ? headerUser : null);

        var roles = ResolveRoles(context.User, authOptions);

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
            IsHeaderSpoofAttempt = isSpoofAttempt
        };
    }

    private static bool IsHeaderFallbackAllowed(ClaimsPrincipal principal, AuthOptions authOptions, IWebHostEnvironment environment)
    {
        var isDevelopment = string.Equals(environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
        var allowByEnvironment = isDevelopment && authOptions.AllowHeaderFallback;
        var gatewayClaim = GetClaim(principal, authOptions.TrustedGatewayClaimName);
        var allowByGateway = IsTruthy(gatewayClaim);
        return allowByEnvironment || allowByGateway;
    }

    private string ResolveLanguage(HttpContext context)
    {
        var language = NormalizeLanguage(GetHeader(context, LanguageHeader));
        if (!string.IsNullOrWhiteSpace(language))
        {
            return language;
        }

        language = NormalizeLanguage(GetHeader(context, AcceptLanguageHeader));
        if (!string.IsNullOrWhiteSpace(language))
        {
            return language;
        }

        language = NormalizeLanguage(_localizationOptions.DefaultLanguage);
        return string.IsNullOrWhiteSpace(language) ? "en" : language;
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var header = GetHeader(context, CorrelationHeader);
        if (!string.IsNullOrWhiteSpace(header))
        {
            return header.Trim();
        }

        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            return traceId;
        }

        return context.TraceIdentifier;
    }

    private static string ResolveConversationId(HttpContext context, string correlationId)
    {
        var header = GetHeader(context, ConversationHeader);
        return string.IsNullOrWhiteSpace(header) ? correlationId : header.Trim();
    }

    private static string ResolveTraceId(string correlationId)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(traceId) ? correlationId : traceId;
    }

    private static string[] ResolveRoles(ClaimsPrincipal principal, AuthOptions authOptions)
    {
        var roleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(authOptions.RoleClaimName))
        {
            foreach (var role in GetRolesFromClaims(principal, authOptions.RoleClaimName))
            {
                AddRole(roleSet, role);
            }
        }

        return roleSet.ToArray();
    }

    private static IEnumerable<string> GetRolesFromClaims(ClaimsPrincipal principal, string claimType)
    {
        if (principal is null)
        {
            return Array.Empty<string>();
        }

        if (string.IsNullOrWhiteSpace(claimType))
        {
            return Array.Empty<string>();
        }

        return principal.FindAll(claimType)
            .SelectMany(claim => SplitRoles(claim.Value));
    }

    private static IEnumerable<string> SplitRoles(string? roles)
    {
        return string.IsNullOrWhiteSpace(roles)
            ? Array.Empty<string>()
            : roles.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void AddRole(HashSet<string> roleSet, string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        roleSet.Add(role.Trim());
    }

    private static string? GetClaim(ClaimsPrincipal? principal, string claimType)
    {
        if (principal is null || string.IsNullOrWhiteSpace(claimType))
        {
            return null;
        }

        return principal.FindFirst(claimType)?.Value?.Trim();
    }

    private static string? GetFirstHeader(HttpContext context, string[]? headerKeys)
    {
        if (headerKeys is null)
        {
            return null;
        }

        foreach (var key in headerKeys)
        {
            var value = GetHeader(context, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static bool IsMismatch(string? claimValue, string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(claimValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        return !string.Equals(claimValue.Trim(), headerValue.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return string.Equals(value.Trim(), "1", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetHeader(HttpContext context, string name)
        => context.Request.Headers.TryGetValue(name, out var values) ? values.ToString() : null;

    private static string? NormalizeIdentity(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var commaIndex = trimmed.IndexOf(',');
        if (commaIndex >= 0)
        {
            trimmed = trimmed[..commaIndex];
        }

        var semicolonIndex = trimmed.IndexOf(';');
        if (semicolonIndex >= 0)
        {
            trimmed = trimmed[..semicolonIndex];
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed.Trim();
    }
}

public sealed class IdentityResolutionResult
{
    public string? TenantId { get; init; }
    public string? UserId { get; init; }
    public string[] Roles { get; init; } = Array.Empty<string>();
    public string Language { get; init; } = "en";
    public string CorrelationId { get; init; } = string.Empty;
    public string ConversationId { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public bool IsHeaderSpoofAttempt { get; init; }
}
