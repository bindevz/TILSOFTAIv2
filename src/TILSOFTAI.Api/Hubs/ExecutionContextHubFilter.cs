using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Security;
using TILSOFTAI.Infrastructure.ExecutionContext;

namespace TILSOFTAI.Api.Hubs;

/// <summary>
/// Hub filter that sets execution context per SignalR invocation.
/// Prevents tenant/user bleed across concurrent hub method calls.
/// </summary>
public sealed class ExecutionContextHubFilter : IHubFilter
{
    private readonly ExecutionContextAccessor _contextAccessor;
    private readonly HubIdentityResolutionPolicy _hubIdentityPolicy;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<ExecutionContextHubFilter> _logger;

    public ExecutionContextHubFilter(
        ExecutionContextAccessor contextAccessor,
        HubIdentityResolutionPolicy hubIdentityPolicy,
        IOptions<AuthOptions> authOptions,
        ILogger<ExecutionContextHubFilter> logger)
    {
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _hubIdentityPolicy = hubIdentityPolicy ?? throw new ArgumentNullException(nameof(hubIdentityPolicy));
        _authOptions = authOptions?.Value ?? throw new ArgumentNullException(nameof(authOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        // Resolve identity using policy (claims-first, no header fallback)
        var result = BuildExecutionContext(invocationContext.Context);
        
        // Fail-closed: Require valid tenant and user claims
        if (string.IsNullOrWhiteSpace(result.TenantId) || string.IsNullOrWhiteSpace(result.UserId))
        {
            throw new TilsoftApiException(
                ErrorCode.Unauthenticated,
                StatusCodes.Status401Unauthorized,
                detail: "SignalR invocation requires valid tenant and user claims");
        }

        // Set execution context from resolved identity
        var context = new TilsoftExecutionContext
        {
            TenantId = result.TenantId,
            UserId = result.UserId,
            Roles = result.Roles,
            CorrelationId = result.CorrelationId,
            ConversationId = result.ConversationId,
            TraceId = result.TraceId,
            RequestId = result.RequestId,
            Language = result.Language
        };

        _contextAccessor.Set(context);

        try
        {
            return await next(invocationContext);
        }
        finally
        {
            // Clear context to prevent leakage
            _contextAccessor.Set(null!);
        }
    }

    private IdentityResolutionResult BuildExecutionContext(HubCallerContext hubContext)
    {
        return _hubIdentityPolicy.ResolveForHub(hubContext, _authOptions);
    }


}
