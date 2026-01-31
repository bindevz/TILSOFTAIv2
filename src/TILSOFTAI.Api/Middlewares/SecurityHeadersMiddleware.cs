namespace TILSOFTAI.Api.Middlewares;

/// <summary>
/// Adds security headers to all HTTP responses.
/// Implements defense-in-depth headers for production deployments.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent MIME type sniffing
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        
        // Prevent clickjacking attacks
        context.Response.Headers["X-Frame-Options"] = "DENY";
        
        // Control referrer information leakage
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        
        // Disable potentially dangerous browser features
        context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        
        await _next(context);
    }
}
