using Microsoft.Extensions.DependencyInjection;
using TILSOFTAI.Agents;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Agents.Domain;
using TILSOFTAI.Approvals;
using TILSOFTAI.Orchestration.Analytics;
using TILSOFTAI.Orchestration.Pipeline;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Supervisor.Classification;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestrationEngine(this IServiceCollection services)
    {
        // Sprint 2: Supervisor runtime with intent classification
        services.AddSingleton<IIntentClassifier, KeywordIntentClassifier>();
        services.AddSingleton<ISupervisorRuntime, SupervisorRuntime>();

        // Sprint 1 compatibility facade (deprecated)
        services.AddSingleton<IOrchestrationEngine, OrchestrationEngine>();

        // Sprint 2: Legacy bridge — single shared pipeline delegation point
        services.AddSingleton<LegacyChatPipelineBridge>();
        services.AddSingleton<ChatPipeline>();

        // Sprint 2: Domain agents
        services.AddSingleton<IDomainAgent, AccountingAgent>();
        services.AddSingleton<IDomainAgent, WarehouseAgent>();
        services.AddSingleton<IDomainAgent, LegacyChatDomainAgent>(); // catch-all fallback

        // Agent registry
        services.AddSingleton<IAgentRegistry, DomainAgentRegistry>();

        // Tool adapter infrastructure
        services.AddSingleton<IToolAdapterRegistry, ToolAdapterRegistry>();

        // Approval engine
        services.AddSingleton<IApprovalEngine, ApprovalEngine>();

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
