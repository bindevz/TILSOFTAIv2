using System.Diagnostics;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Auth;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Infrastructure.ExecutionContext;
using System.Security.Claims;

namespace TILSOFTAI.Api.Middlewares;

public sealed class ExecutionContextMiddleware : IMiddleware
{
    private readonly ExecutionContextAccessor _accessor;
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly IOptions<LocalizationOptions> _localizationOptions;

    public ExecutionContextMiddleware(
        ExecutionContextAccessor accessor,
        IOptions<AuthOptions> authOptions,
        IOptions<LocalizationOptions> localizationOptions)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
        _localizationOptions = localizationOptions ?? throw new ArgumentNullException(nameof(localizationOptions));
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var authOptions = _authOptions.Value;
        
        // Resolve tenant identity: claims first, then header fallback if enabled
        var tenantId = ResolveIdentityFromClaimOrHeader(
            context,
            authOptions.TenantClaimName,
            authOptions.HeaderTenantKeys,
            authOptions.AllowHeaderFallback);
        
        // Resolve user identity: claims first, then header fallback if enabled
        var userId = ResolveIdentityFromClaimOrHeader(
            context,
            authOptions.UserIdClaimName,
            authOptions.HeaderUserKeys,
            authOptions.AllowHeaderFallback);

        // For authenticated endpoints, require tenant and user; allow system defaults for health endpoint
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
        {
            if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
            {
                tenantId ??= "system";
                userId ??= "system";
            }
            else
            {
                throw new UnauthorizedAccessException("tenant_id and user_id are required.");
            }
        }

        // Resolve correlation ID: prefer header, fallback to new GUID
        var correlationId = GetHeader(context, "X-Correlation-Id");
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        }

        // Resolve conversation ID: prefer header, fallback to correlation ID
        var conversationId = GetHeader(context, "X-Conversation-Id");
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            conversationId = correlationId;
        }

        // Resolve language: X-Lang > Accept-Language > Default
        var language = ExecutionContextResolver.ResolveLanguage(
            ExecutionContextResolver.GetClaim(context.User, TilsoftClaims.Language),
            GetHeader(context, "X-Lang"),
            GetHeader(context, "Accept-Language"),
            _localizationOptions.Value);

        // Resolve roles: merge from claims and headers (if fallback enabled)
        var roleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Add roles from claims (support multiple claim types and JSON arrays)
        AddRoles(roleSet, GetRolesFromClaims(context.User, new[]
        {
            authOptions.RoleClaimName,
            TilsoftClaims.Roles,
            TilsoftClaims.Role,
            ClaimTypes.Role
        }));
        
        // Add roles from headers (only if fallback is enabled)
        if (authOptions.AllowHeaderFallback)
        {
            foreach (var headerKey in authOptions.HeaderRolesKeys)
            {
                var rolesHeader = GetHeader(context, headerKey);
                if (!string.IsNullOrWhiteSpace(rolesHeader))
                {
                    AddRoles(roleSet, rolesHeader.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }
            }
        }

        var executionContext = new TilsoftExecutionContext
        {
            TenantId = tenantId.Trim(),
            UserId = userId.Trim(),
            Roles = roleSet.ToArray(),
            CorrelationId = correlationId,
            ConversationId = conversationId,
            RequestId = Guid.NewGuid().ToString("N"),
            TraceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            Language = language
        };

        _accessor.Set(executionContext);

        await next(context);
    }

    private string? ResolveIdentityFromClaimOrHeader(
        HttpContext context,
        string claimName,
        string[] headerKeys,
        bool allowHeaderFallback)
    {
        // 1. Try to get from claim
        var claimValue = ExecutionContextResolver.GetClaim(context.User, claimName);
        if (!string.IsNullOrWhiteSpace(claimValue))
        {
            // Security: ALWAYS check if headers conflict with claims (prevent spoofing)
            // This check happens regardless of AllowHeaderFallback setting
            foreach (var headerKey in headerKeys)
            {
                var headerValue = GetHeader(context, headerKey);
                if (!string.IsNullOrWhiteSpace(headerValue) &&
                    !string.Equals(claimValue.Trim(), headerValue.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException($"{claimName} claim does not match {headerKey} header.");
                }
            }
            return claimValue.Trim();
        }

        // 2. If no claim, try headers (only if fallback is enabled)
        if (allowHeaderFallback)
        {
            foreach (var headerKey in headerKeys)
            {
                var headerValue = GetHeader(context, headerKey);
                if (!string.IsNullOrWhiteSpace(headerValue))
                {
                    return headerValue.Trim();
                }
            }
        }

        // 3. Nothing found
        return null;
    }

    private static string? GetHeader(HttpContext context, string name)
    {
        return context.Request.Headers.TryGetValue(name, out var values)
            ? values.ToString()
            : null;
    }

    private static void AddRoles(HashSet<string> roleSet, IEnumerable<string> roles)
    {
        foreach (var role in roles)
        {
            var trimmed = role.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                roleSet.Add(trimmed);
            }
        }
    }

    private static IEnumerable<string> GetRolesFromClaims(ClaimsPrincipal? principal, IEnumerable<string> claimTypes)
    {
        if (principal is null)
        {
            return Array.Empty<string>();
        }

        var types = claimTypes
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var roleClaims = types
            .SelectMany(type => principal.FindAll(type))
            .SelectMany(claim => claim.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return roleClaims;
    }
}
