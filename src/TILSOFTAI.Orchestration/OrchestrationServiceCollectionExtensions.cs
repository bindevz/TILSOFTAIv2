using Microsoft.Extensions.DependencyInjection;
using TILSOFTAI.Agents;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Approvals;
using TILSOFTAI.Orchestration.Analytics;
using TILSOFTAI.Orchestration.Pipeline;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestrationEngine(this IServiceCollection services)
    {
        services.AddSingleton<ISupervisorRuntime, SupervisorRuntime>();
        services.AddSingleton<IOrchestrationEngine, OrchestrationEngine>();
        services.AddSingleton<IDomainAgent, LegacyChatDomainAgent>();
        services.AddSingleton<IAgentRegistry, DomainAgentRegistry>();
        services.AddSingleton<IToolAdapterRegistry, ToolAdapterRegistry>();
        services.AddSingleton<IApprovalEngine, ApprovalEngine>();
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
