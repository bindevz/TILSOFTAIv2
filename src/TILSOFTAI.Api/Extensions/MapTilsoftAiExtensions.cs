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
        
        // Exception handling must be outermost to ensure all errors are envelope-shaped
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        
        // CORS (if enabled via configuration)
        var corsOptions = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;
        if (corsOptions.Enabled)
        {
            app.UseCors(policy =>
            {
                policy.WithOrigins(corsOptions.AllowedOrigins)
                      .WithMethods(corsOptions.AllowedMethods)
                      .WithHeaders(corsOptions.AllowedHeaders);

                if (corsOptions.AllowCredentials)
                {
                    policy.AllowCredentials();
                }
            });
        }
        
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<RequestSizeLimitMiddleware>();
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
