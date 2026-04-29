namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiCorsExtensions
{
    public static IServiceCollection AddTilsoftAiCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors();
        return services;
    }
}
