using TILSOFTAI.Api.Hubs;
using TILSOFTAI.Api.Middlewares;

namespace TILSOFTAI.Api.Extensions;

public static class MapTilsoftAiExtensions
{
    public static WebApplication MapTilsoftAi(this WebApplication app)
    {
        app.UseRouting();
        app.UseMiddleware<RequestSizeLimitMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseMiddleware<ExecutionContextMiddleware>();

        app.MapControllers();
        app.MapHub<ChatHub>("/hubs/chat");
        
        // Health endpoints for operational readiness
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false // No checks for liveness - always returns 200 if process is up
        }).AllowAnonymous();
        
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        }).AllowAnonymous();
        
        // Keep backward compatible /health endpoint
        app.MapHealthChecks("/health").AllowAnonymous();

        return app;
    }
}
