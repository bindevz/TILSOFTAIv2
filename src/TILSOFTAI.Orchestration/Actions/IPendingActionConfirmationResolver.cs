using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Answering;

namespace TILSOFTAI.Orchestration.Actions;

public interface IPendingActionConfirmationResolver
{
    Task<PendingActionConfirmationResult?> TryResolveAsync(
        string input,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken);
}

public sealed record PendingActionConfirmationResult(bool Handled, AssistantAnswer? Answer);
