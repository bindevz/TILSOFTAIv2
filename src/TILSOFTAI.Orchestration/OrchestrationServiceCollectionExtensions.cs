using Microsoft.Extensions.DependencyInjection;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestrationEngine(this IServiceCollection services)
    {
        services.AddSingleton<IOrchestrationEngine, OrchestrationEngine>();
        services.AddSingleton<ChatPipeline>();
        return services;
    }
}
