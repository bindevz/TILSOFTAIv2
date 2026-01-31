using System.Buffers;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;

namespace TILSOFTAI.Api.Middlewares;

/// <summary>
/// Middleware to enforce maximum request body size for chat endpoints.
/// Validates Content-Length header and buffers chunked requests to prevent bypass.
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

            // Fast path: Content-Length present and exceeds limit
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

            // Slow path: Content-Length missing (chunked transfer or HTTP/1.0)
            // Use bounded buffering to prevent unbounded reads
            if (!contentLength.HasValue)
            {
                await EnforceChunkedRequestLimitAsync(context);
            }
        }

        await _next(context);
    }

    private async Task EnforceChunkedRequestLimitAsync(HttpContext context)
    {
        // Enable buffering with limits to allow multiple reads
        context.Request.EnableBuffering(
            bufferThreshold: _chatOptions.MaxRequestBytes,
            bufferLimit: _chatOptions.MaxRequestBytes + 1);

        var buffer = ArrayPool<byte>.Shared.Rent(8192); // 8KB chunks
        try
        {
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await context.Request.Body.ReadAsync(buffer, 0, buffer.Length, context.RequestAborted)) > 0)
            {
                totalBytesRead += bytesRead;

                if (totalBytesRead > _chatOptions.MaxRequestBytes)
                {
                    _logger.LogWarning(
                        "Chunked request to {Path} rejected: body size {TotalBytes} exceeds limit {MaxRequestBytes}",
                        context.Request.Path,
                        totalBytesRead,
                        _chatOptions.MaxRequestBytes);

                    throw new TilsoftApiException(
                        ErrorCode.RequestTooLarge,
                        StatusCodes.Status413RequestEntityTooLarge,
                        detail: new
                        {
                            maxRequestBytes = _chatOptions.MaxRequestBytes,
                            actualBytes = totalBytesRead
                        });
                }
            }

            // Reset stream position for downstream handlers
            context.Request.Body.Position = 0;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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
