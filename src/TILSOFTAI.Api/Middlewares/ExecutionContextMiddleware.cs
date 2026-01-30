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
            Roles = identity.Roles ?? Array.Empty<string>(),
            CorrelationId = identity.CorrelationId,
            ConversationId = identity.ConversationId,
            RequestId = identity.RequestId,
            TraceId = identity.TraceId,
            Language = identity.Language
        };

        _accessor.Set(executionContext);

        await next(context);
    }
}
