using TILSOFTAI.Api.Hubs;
using TILSOFTAI.Api.Middlewares;
using TILSOFTAI.Domain.Configuration;
using Microsoft.Extensions.Options;

namespace TILSOFTAI.Api.Extensions;

public static class MapTilsoftAiExtensions
{
    public static WebApplication MapTilsoftAi(this WebApplication app)
    {
        app.UseRouting();
        
        // Security headers for all responses
        app.UseMiddleware<SecurityHeadersMiddleware>();
        
        // CORS (if enabled via configuration)
        var corsOptions = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;
        if (corsOptions.Enabled)
        {
            app.UseCors("TilsoftCorsPolicy");
        }
        
        app.UseMiddleware<RequestSizeLimitMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseMiddleware<ExecutionContextMiddleware>();

        app.MapControllers().RequireAuthorization();
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
