using Microsoft.Extensions.DependencyInjection;
using TILSOFTAI.Agents;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Agents.Domain;
using TILSOFTAI.Approvals;
using TILSOFTAI.Orchestration.Analytics;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Orchestration.Pipeline;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Supervisor.Classification;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddSupervisorRuntime(this IServiceCollection services)
    {
        // Supervisor runtime with intent classification
        services.AddSingleton<IIntentClassifier, KeywordIntentClassifier>();
        services.AddSingleton<RuntimeExecutionInstrumentation>();
        services.AddSingleton<ISupervisorRuntime, SupervisorRuntime>();

        // Legacy bridge is fallback-only; native capability execution is owned by domain agents.
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
        services.AddSingleton<IDomainAgent, GeneralChatAgent>();  // Sprint 7: supervisor-native general fallback

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
