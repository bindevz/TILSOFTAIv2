using Microsoft.Extensions.DependencyInjection;
using TILSOFTAI.Orchestration.Analytics;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestrationEngine(this IServiceCollection services)
    {
        services.AddSingleton<IOrchestrationEngine, OrchestrationEngine>();
        services.AddSingleton<ChatPipeline>();
        
        // Analytics components
        services.AddSingleton<AnalyticsIntentDetector>();
        services.AddSingleton<AnalyticsCache>();
        services.AddSingleton<AnalyticsPersistence>();
        services.AddSingleton<InsightRenderer>();
        services.AddSingleton<IInsightAssemblyService, InsightAssemblyService>();
        services.AddSingleton<AnalyticsOrchestrator>();
        
        return services;
    }
}
