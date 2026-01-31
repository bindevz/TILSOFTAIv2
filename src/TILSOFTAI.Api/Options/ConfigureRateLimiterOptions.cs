using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Options;

/// <summary>
/// Configures RateLimiter options using validated IOptions&lt;RateLimitOptions&gt;.
/// </summary>
public sealed class ConfigureRateLimiterOptions : IConfigureOptions<RateLimiterOptions>
{
    private readonly IOptions<RateLimitOptions> _rateLimitOptions;

    public ConfigureRateLimiterOptions(IOptions<RateLimitOptions> rateLimitOptions)
    {
        _rateLimitOptions = rateLimitOptions ?? throw new ArgumentNullException(nameof(rateLimitOptions));
    }

    public void Configure(RateLimiterOptions options)
    {
        var rateLimitOpts = _rateLimitOptions.Value;

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
