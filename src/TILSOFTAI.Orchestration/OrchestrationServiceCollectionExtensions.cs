using Microsoft.Extensions.DependencyInjection;
using TILSOFTAI.Approvals;
using TILSOFTAI.Orchestration.Actions;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Answering.Narration;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Orchestration.Semantic;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddSupervisorRuntime(this IServiceCollection services)
    {
        services.AddModelOnlyCapabilities();
        services.AddOfficialAgentFrameworkCore();
        services.AddCapabilityExecutionBoundary();
        services.AddAnswerComposer();
        services.AddPendingActionState();

        return services;
    }

    private static IServiceCollection AddModelOnlyCapabilities(this IServiceCollection services)
    {
        services.AddSingleton<CapabilityArgumentMapper>();
        services.AddSingleton<CapabilityExecutionPolicy>();
        return services;
    }

    private static IServiceCollection AddOfficialAgentFrameworkCore(this IServiceCollection services)
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
        services.AddSingleton<ISupervisorRuntime, SupervisorRuntime>();
        return services;
    }

    private static IServiceCollection AddCapabilityExecutionBoundary(this IServiceCollection services)
    {
        services.AddSingleton<ICapabilityExecutionFacade, CapabilityExecutionFacade>();
        services.AddSingleton<ICompositeCapabilityExecutor, CompositeCapabilityExecutor>();
        services.AddSingleton<IToolAdapterRegistry, ToolAdapterRegistry>();
        return services;
    }

    private static IServiceCollection AddAnswerComposer(this IServiceCollection services)
    {
        services.AddSingleton<RawJsonAnswerComposer>();
        services.AddSingleton<AnswerNarrationPromptBuilder>();
        services.AddSingleton<AnswerNarrationResponseParser>();
        services.AddSingleton<GenericSchemaSummaryFallback>();
        services.AddSingleton<IAnswerNarrationService, AgentAnswerNarrationService>();
        services.AddSingleton<IAnswerComposer, StructuredAnswerComposer>();
        return services;
    }

    private static IServiceCollection AddPendingActionState(this IServiceCollection services)
    {
        services.AddSingleton<IPendingActionConfirmationResolver, PendingActionConfirmationResolver>();
        services.AddSingleton<IApprovalEngine, ApprovalEngine>();
        return services;
    }
}
