using Microsoft.Extensions.DependencyInjection;
using TILSOFTAI.Agents;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Agents.Domain;
using TILSOFTAI.Approvals;
using TILSOFTAI.Orchestration.Actions;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Analytics;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Orchestration.Semantic;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Supervisor.Classification;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddSupervisorRuntime(this IServiceCollection services)
    {
        services.AddModelOnlyCapabilitySource();
        services.AddOfficialAgentRouting();
        services.AddAnswerComposer();
        services.AddPendingActions();
        services.AddLegacyFallbackOnlyServices();

        return services;
    }

    private static IServiceCollection AddModelOnlyCapabilitySource(this IServiceCollection services)
    {
        services.AddSingleton<ICapabilitySource>(
            new StaticCapabilitySource("static-model", ModelCapabilities.All));
        services.AddSingleton<ICapabilityRegistry, CompositeCapabilityRegistry>();
        services.AddSingleton<CapabilityArgumentMapper>();
        services.AddSingleton<CapabilityExecutionPolicy>();
        services.AddSingleton<ICapabilityExecutionFacade, CapabilityExecutionFacade>();
        services.AddSingleton<ICompositeCapabilityExecutor, CompositeCapabilityExecutor>();
        services.AddSingleton<IToolAdapterRegistry, ToolAdapterRegistry>();
        return services;
    }

    private static IServiceCollection AddOfficialAgentRouting(this IServiceCollection services)
    {
        services.AddSingleton<RuntimeExecutionInstrumentation>();
        services.AddSingleton<IHardSignalExtractor, HardSignalExtractor>();
        services.AddSingleton<IDomainGate, DomainGate>();
        services.AddSingleton<AgentRunOptionsFactory>();
        services.AddSingleton<CapabilityToolDescriptionBuilder>();
        services.AddSingleton<CapabilityParameterSchemaBuilder>();
        services.AddSingleton<ICapabilityToolDescriptorFactory, CapabilityToolDescriptorFactory>();
        services.AddSingleton<IOfficialAgentFunctionProvider, DynamicFunctionToolFactory>();
        services.AddSingleton<IOfficialAgentProviderFactory, OfficialAgentProviderFactory>();
        services.AddSingleton<IOfficialMicrosoftAgentRuntime, OfficialMicrosoftAgentRuntime>();
        services.AddSingleton<ISemanticCapabilityRetriever, SemanticCapabilityRetriever>();
        services.AddSingleton<ICapabilityCandidateSelector, SemanticCapabilityCandidateSelector>();
        services.AddSingleton<IOfficialAgentToolRouter, OfficialAgentToolRouter>();
        services.AddSingleton<IAgentToolRouter>(sp => sp.GetRequiredService<IOfficialAgentToolRouter>());
        return services;
    }

    private static IServiceCollection AddAnswerComposer(this IServiceCollection services)
    {
        services.AddSingleton<RawJsonAnswerComposer>();
        services.AddSingleton<AiSummaryService>();
        services.AddSingleton<IAnswerComposer, StructuredAnswerComposer>();
        return services;
    }

    private static IServiceCollection AddPendingActions(this IServiceCollection services)
    {
        services.AddSingleton<IPendingActionConfirmationResolver, PendingActionConfirmationResolver>();
        services.AddSingleton<IApprovalEngine, ApprovalEngine>();
        return services;
    }

    private static IServiceCollection AddLegacyFallbackOnlyServices(this IServiceCollection services)
    {
        services.AddSingleton<IIntentClassifier, KeywordIntentClassifier>();
        services.AddSingleton<ICapabilityResolver, StructuredCapabilityResolver>();
        services.AddSingleton<IDomainAgent, GeneralChatAgent>();
        services.AddSingleton<IAgentRegistry, DomainAgentRegistry>();
        services.AddSingleton<ISupervisorRuntime, SupervisorRuntime>();

        services.AddSingleton<AnalyticsIntentDetector>();
        services.AddSingleton<AnalyticsCache>();
        services.AddSingleton<AnalyticsPersistence>();
        services.AddSingleton<InsightRenderer>();
        services.AddSingleton<IInsightAssemblyService, InsightAssemblyService>();
        services.AddSingleton<AnalyticsOrchestrator>();
        return services;
    }
}
