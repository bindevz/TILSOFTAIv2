namespace TILSOFTAI.Api.Middlewares;

/// <summary>
/// Handles Chrome's Private Network Access (PNA) specification.
/// When a public website (e.g., http://tsl-app.auvietsoft.vn) makes requests
/// to a private/local IP (e.g., 192.168.x.x), Chrome requires the server to
/// explicitly opt-in by responding with Access-Control-Allow-Private-Network.
/// This middleware handles both preflight (OPTIONS) and regular requests.
/// See: https://developer.chrome.com/blog/private-network-access-preflight
/// </summary>
public sealed class PrivateNetworkAccessMiddleware
{
    private readonly RequestDelegate _next;

    public PrivateNetworkAccessMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Always add the PNA header to signal the server allows private network access
        context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";

        // Handle PNA preflight: browser sends OPTIONS with this header
        if (HttpMethods.IsOptions(context.Request.Method)
            && context.Request.Headers.ContainsKey("Access-Control-Request-Private-Network"))
        {
            // Ensure standard CORS preflight headers are present
            var origin = context.Request.Headers.Origin.FirstOrDefault();
            if (!string.IsNullOrEmpty(origin))
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            }
            else
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            }

            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With";
            context.Response.Headers["Access-Control-Max-Age"] = "86400"; // Cache preflight for 24h
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return; // Short-circuit — do not pass to next middleware
        }

        await _next(context);
    }
}
