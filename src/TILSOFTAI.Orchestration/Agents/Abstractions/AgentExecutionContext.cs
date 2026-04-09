using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Agents.Abstractions;

public sealed class AgentExecutionContext
{
    public TilsoftExecutionContext RuntimeContext { get; init; } = new();
    public IApprovalEngine ApprovalEngine { get; init; } = default!;

    /// <summary>
    /// Sprint 2: provides access to tool adapters for agents that need direct adapter execution.
    /// Currently unused by skeleton agents (Option 4); available for future domain-native execution.
    /// </summary>
    public IToolAdapterRegistry? ToolAdapterRegistry { get; init; }

    public static AgentExecutionContext FromRuntimeContext(
        TilsoftExecutionContext runtimeContext,
        IApprovalEngine approvalEngine,
        IToolAdapterRegistry? toolAdapterRegistry = null) => new()
    {
        RuntimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext)),
        ApprovalEngine = approvalEngine ?? throw new ArgumentNullException(nameof(approvalEngine)),
        ToolAdapterRegistry = toolAdapterRegistry
    };
}
