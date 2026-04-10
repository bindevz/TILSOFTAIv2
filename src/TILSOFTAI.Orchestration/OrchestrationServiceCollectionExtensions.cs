using Microsoft.Extensions.DependencyInjection;
using TILSOFTAI.Agents;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Agents.Domain;
using TILSOFTAI.Approvals;
using TILSOFTAI.Orchestration.Analytics;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Pipeline;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Supervisor.Classification;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestrationEngine(this IServiceCollection services)
    {
        // Supervisor runtime with intent classification
        services.AddSingleton<IIntentClassifier, KeywordIntentClassifier>();
        services.AddSingleton<ISupervisorRuntime, SupervisorRuntime>();

        // Sprint 1 compatibility facade (deprecated — to be removed when all controllers use ISupervisorRuntime)
        services.AddSingleton<IOrchestrationEngine, OrchestrationEngine>();

        // Legacy bridge — transitional shared pipeline delegation point
        // Sprint 4: only used as fallback when no native capability matches
        services.AddSingleton<LegacyChatPipelineBridge>();
        services.AddSingleton<ChatPipeline>();

        // Sprint 4: Capability registry — seeded with warehouse capabilities
        services.AddSingleton<ICapabilityRegistry>(
            new InMemoryCapabilityRegistry(WarehouseCapabilities.All));

        // Domain agents
        services.AddSingleton<IDomainAgent, AccountingAgent>();   // still bridge-only (Sprint 4)
        services.AddSingleton<IDomainAgent, WarehouseAgent>();    // Sprint 4: native capability path
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
