namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiExtensions
{
    public static IServiceCollection AddTilsoftAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTilsoftAiOptions(configuration);
        services.AddTilsoftAiAuth(configuration);
        services.AddTilsoftAiCors(configuration);
        services.AddTilsoftAiTelemetry(configuration);
        services.AddTilsoftAiSql(configuration);
        services.AddTilsoftAiCaching(configuration);
        services.AddTilsoftAiLocalAi(configuration);
        services.AddTilsoftAiHealthChecks(configuration);
        services.AddTilsoftAiOrchestration(configuration);

        return services;
    }

    public static IServiceCollection AddTilsoftAi(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        return services.AddTilsoftAi(configuration);
    }
}
