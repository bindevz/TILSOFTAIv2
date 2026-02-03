using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Options;

/// <summary>
/// Configures RateLimiter options using validated IOptions&lt;RateLimitOptions&gt;.
/// </summary>
public sealed class ConfigureRateLimiterOptions : IConfigureOptions<RateLimiterOptions>
{
    private readonly IOptions<RateLimitOptions> _rateLimitOptions;
    private readonly IAuditLogger _auditLogger;
    private readonly IOptions<AuthOptions> _authOptions;

    public ConfigureRateLimiterOptions(
        IOptions<RateLimitOptions> rateLimitOptions,
        IAuditLogger auditLogger,
        IOptions<AuthOptions> authOptions)
    {
        _rateLimitOptions = rateLimitOptions ?? throw new ArgumentNullException(nameof(rateLimitOptions));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
    }

    public void Configure(RateLimiterOptions options)
    {
        var rateLimitOpts = _rateLimitOptions.Value;

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = (context, ct) =>
        {
            var httpContext = context.HttpContext;
            var path = httpContext.Request.Path.Value ?? string.Empty;
            var correlationId = httpContext.TraceIdentifier;
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty;

            var tenantId = httpContext.User.FindFirst(_authOptions.Value.TenantClaimName)?.Value ?? string.Empty;
            var userId = httpContext.User.FindFirst(_authOptions.Value.UserIdClaimName)?.Value ?? string.Empty;

            _auditLogger.LogSecurityEvent(SecurityAuditEvent.RateLimitExceeded(
                tenantId,
                userId,
                correlationId,
                ipAddress,
                userAgent.Length > 500 ? userAgent[..500] : userAgent,
                path,
                0, // Request count not available here
                rateLimitOpts.PermitLimit));

            return ValueTask.CompletedTask;
        };

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            // Exempt health endpoints from rate limiting to prevent monitoring failures
            var path = httpContext.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                return RateLimitPartition.GetNoLimiter("health");
            }

            var factory = new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimitOpts.PermitLimit,
                QueueLimit = rateLimitOpts.QueueLimit,
                Window = TimeSpan.FromSeconds(rateLimitOpts.WindowSeconds)
            };

            var partitionKey = httpContext.User.Identity?.Name
                               ?? httpContext.Connection.RemoteIpAddress?.ToString()
                               ?? "anonymous";

            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => factory);
        });
    }
}
