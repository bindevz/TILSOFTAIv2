using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Security;
using TILSOFTAI.Infrastructure.ExecutionContext;

namespace TILSOFTAI.Api.Middlewares;

public sealed class ExecutionContextMiddleware : IMiddleware
{
    private readonly ExecutionContextAccessor _accessor;
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly IdentityResolutionPolicy _identityPolicy;
    private readonly IWebHostEnvironment _hostEnvironment;

    public ExecutionContextMiddleware(
        ExecutionContextAccessor accessor,
        IOptions<AuthOptions> authOptions,
        IdentityResolutionPolicy identityPolicy,
        IWebHostEnvironment hostEnvironment)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
        _identityPolicy = identityPolicy ?? throw new ArgumentNullException(nameof(identityPolicy));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var authOptions = _authOptions.Value;
        
        // Detect if endpoint requires authentication (has [Authorize] and not [AllowAnonymous])
        var endpoint = context.GetEndpoint();
        var requiresAuth = endpoint?.Metadata.GetMetadata<IAuthorizeData>() != null
                        && endpoint?.Metadata.GetMetadata<IAllowAnonymous>() == null;
        var identity = _identityPolicy.ResolveForRequest(context, authOptions, _hostEnvironment);

        if (identity.IsHeaderSpoofAttempt)
        {
            throw new TilsoftApiException(
                ErrorCode.TenantMismatch,
                StatusCodes.Status403Forbidden,
                detail: new { suspicious_identity_header = true });
        }

        if (requiresAuth && (string.IsNullOrWhiteSpace(identity.TenantId) || string.IsNullOrWhiteSpace(identity.UserId)))
        {
            throw new UnauthorizedAccessException(ErrorCode.Unauthenticated);
        }

        var tenantId = identity.TenantId;
        var userId = identity.UserId;

        if (!requiresAuth)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = "public";
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                userId = string.Empty;
            }
        }

        var executionContext = new TilsoftExecutionContext
        {
            TenantId = tenantId?.Trim() ?? string.Empty,
            UserId = userId?.Trim() ?? string.Empty,
            Roles = NormalizeRoles(identity.Roles),
            CorrelationId = identity.CorrelationId,
            ConversationId = identity.ConversationId,
            RequestId = identity.RequestId,
            TraceId = identity.TraceId,
            Language = identity.Language,
            IpAddress = GetClientIpAddress(context),
            UserAgent = GetUserAgent(context)
        };

        _accessor.Set(executionContext);

        // Sync LogContext with ExecutionContext
        var logContext = Domain.Logging.LogContext.Current;
        logContext.TenantId = executionContext.TenantId;
        logContext.UserId = executionContext.UserId;
        logContext.ConversationId = executionContext.ConversationId;
        
        await next(context);
    }

    private static string[] NormalizeRoles(string[]? roles)
    {
        if (roles is null || roles.Length == 0)
        {
            return Array.Empty<string>();
        }

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }

            var trimmed = role.Trim();
            if (trimmed.Length == 0 || ContainsSuspiciousToken(trimmed))
            {
                continue;
            }

            normalized.Add(trimmed);
        }

        return normalized.ToArray();
    }

    private static bool ContainsSuspiciousToken(string role)
    {
        foreach (var ch in role)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check X-Forwarded-For header first (for reverse proxy scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // Take the first IP (original client)
            var firstIp = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return firstIp;
            }
        }

        // Fall back to direct connection IP
        return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    private static string GetUserAgent(HttpContext context)
    {
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;
        // Truncate to reasonable length for storage
        const int maxLength = 500;
        return userAgent.Length > maxLength ? userAgent[..maxLength] : userAgent;
    }
}
