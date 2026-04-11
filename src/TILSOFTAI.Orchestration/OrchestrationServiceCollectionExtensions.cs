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
        #pragma warning disable CS0618 // Obsolete
        services.AddSingleton<IOrchestrationEngine, OrchestrationEngine>();
        #pragma warning restore CS0618

        // Legacy bridge — transitional shared pipeline delegation point
        // Sprint 5: only used as fallback when no native capability matches for either domain
        services.AddSingleton<LegacyChatPipelineBridge>();
        services.AddSingleton<ChatPipeline>();

        // Sprint 5: Capability resolver — structured resolution replacing string matching
        services.AddSingleton<ICapabilityResolver, StructuredCapabilityResolver>();

        // Sprint 5: Capability sources — static fallbacks + configuration-driven
        services.AddSingleton<ICapabilitySource>(
            new StaticCapabilitySource("static-warehouse", WarehouseCapabilities.All));
        services.AddSingleton<ICapabilitySource>(
            new StaticCapabilitySource("static-accounting", AccountingCapabilities.All));
        services.AddSingleton<ICapabilitySource, ConfigurationCapabilitySource>();

        // Sprint 5: Composite capability registry — loads from all ICapabilitySource instances
        services.AddSingleton<ICapabilityRegistry, CompositeCapabilityRegistry>();

        // Domain agents
        services.AddSingleton<IDomainAgent, AccountingAgent>();   // Sprint 5: native capability path
        services.AddSingleton<IDomainAgent, WarehouseAgent>();    // Sprint 4+5: native capability path
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
