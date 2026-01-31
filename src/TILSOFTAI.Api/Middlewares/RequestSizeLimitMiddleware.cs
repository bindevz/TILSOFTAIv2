using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;

namespace TILSOFTAI.Api.Middlewares;

/// <summary>
/// Middleware to enforce maximum request body size for chat endpoints.
/// Validates Content-Length header against Chat:MaxRequestBytes configuration.
/// </summary>
public sealed class RequestSizeLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ChatOptions _chatOptions;
    private readonly ILogger<RequestSizeLimitMiddleware> _logger;

    // Routes that enforce request size limits
    private static readonly HashSet<string> EnforcedRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/chat",
        "/api/chat/stream",
        "/v1/chat/completions"
    };

    public RequestSizeLimitMiddleware(
        RequestDelegate next,
        IOptions<ChatOptions> chatOptions,
        ILogger<RequestSizeLimitMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _chatOptions = chatOptions?.Value ?? throw new ArgumentNullException(nameof(chatOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldEnforceLimit(context))
        {
            var contentLength = context.Request.ContentLength;

            if (contentLength.HasValue && contentLength.Value > _chatOptions.MaxRequestBytes)
            {
                _logger.LogWarning(
                    "Request to {Path} rejected: Content-Length {ContentLength} exceeds limit {MaxRequestBytes}",
                    context.Request.Path,
                    contentLength.Value,
                    _chatOptions.MaxRequestBytes);

                throw new TilsoftApiException(
                    ErrorCode.RequestTooLarge,
                    StatusCodes.Status413RequestEntityTooLarge,
                    detail: new
                    {
                        maxRequestBytes = _chatOptions.MaxRequestBytes,
                        actualBytes = contentLength.Value
                    });
            }
        }

        await _next(context);
    }

    private static bool ShouldEnforceLimit(HttpContext context)
    {
        // Only enforce for POST requests on chat endpoints
        if (!string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return EnforcedRoutes.Contains(context.Request.Path.Value ?? string.Empty);
    }
}
