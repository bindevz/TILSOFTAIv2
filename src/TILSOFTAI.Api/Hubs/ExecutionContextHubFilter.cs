using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Infrastructure.ExecutionContext;

namespace TILSOFTAI.Api.Hubs;

/// <summary>
/// Hub filter that sets execution context per SignalR invocation.
/// Prevents tenant/user bleed across concurrent hub method calls.
/// </summary>
public sealed class ExecutionContextHubFilter : IHubFilter
{
    private readonly ExecutionContextAccessor _contextAccessor;
    private readonly AuthOptions _authOptions;
    private readonly LocalizationOptions _localizationOptions;
    private readonly ILogger<ExecutionContextHubFilter> _logger;

    public ExecutionContextHubFilter(
        ExecutionContextAccessor contextAccessor,
        IOptions<AuthOptions> authOptions,
        IOptions<LocalizationOptions> localizationOptions,
        ILogger<ExecutionContextHubFilter> logger)
    {
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _authOptions = authOptions?.Value ?? throw new ArgumentNullException(nameof(authOptions));
        _localizationOptions = localizationOptions?.Value ?? throw new ArgumentNullException(nameof(localizationOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        // Set execution context from claims for this invocation
        var context = BuildExecutionContext(invocationContext.Context);
        _contextAccessor.Set(context);

        try
        {
            return await next(invocationContext);
        }
        finally
        {
            // Clear context to prevent leakage by setting to empty context
            _contextAccessor.Set(new TilsoftExecutionContext());
        }
    }

    private TilsoftExecutionContext BuildExecutionContext(HubCallerContext hubContext)
    {
        var principal = hubContext.User;
        var correlationId = ResolveCorrelationId();
        var traceId = ResolveTraceId(correlationId);
        var requestId = hubContext.ConnectionId;

        var tenantId = GetClaim(principal, _authOptions.TenantClaimName);
        var userId = GetClaim(principal, _authOptions.UserIdClaimName);
        var roles = ResolveRoles(principal);
        var language = ResolveLanguage(_localizationOptions.DefaultLanguage);

        return new TilsoftExecutionContext
        {
            TenantId = NormalizeIdentity(tenantId) ?? string.Empty,
            UserId = NormalizeIdentity(userId) ?? string.Empty,
            Roles = roles,
            CorrelationId = correlationId,
            ConversationId = correlationId, // Use correlationId as conversationId for SignalR
            TraceId = traceId,
            RequestId = requestId,
            Language = language
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

    private static string ResolveLanguage(string defaultLanguage)
    {
        return string.IsNullOrWhiteSpace(defaultLanguage) ? "en" : defaultLanguage.Trim();
    }

    private string[] ResolveRoles(ClaimsPrincipal? principal)
    {
        if (principal is null || string.IsNullOrWhiteSpace(_authOptions.RoleClaimName))
        {
            return Array.Empty<string>();
        }

        var roleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in principal.FindAll(_authOptions.RoleClaimName))
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
}
