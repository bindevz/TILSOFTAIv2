using TILSOFTAI.Api.Hubs;
using TILSOFTAI.Api.Middlewares;

namespace TILSOFTAI.Api.Extensions;

public static class MapTilsoftAiExtensions
{
    public static WebApplication MapTilsoftAi(this WebApplication app)
    {
        app.UseRouting();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseMiddleware<ExecutionContextMiddleware>();

        app.MapControllers();
        app.MapHub<ChatHub>("/hubs/chat");
        app.MapHealthChecks("/health");

        return app;
    }
}
