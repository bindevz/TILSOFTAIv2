using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Agents.Abstractions;

public sealed class AgentExecutionContext
{
    public TilsoftExecutionContext RuntimeContext { get; init; } = new();
    public IApprovalEngine ApprovalEngine { get; init; } = default!;

    public static AgentExecutionContext FromRuntimeContext(
        TilsoftExecutionContext runtimeContext,
        IApprovalEngine approvalEngine) => new()
    {
        RuntimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext)),
        ApprovalEngine = approvalEngine ?? throw new ArgumentNullException(nameof(approvalEngine))
    };
}
