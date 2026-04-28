namespace TILSOFTAI.Orchestration.Execution;

public interface ICapabilityExecutionFacade
{
    Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);

    Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);

    Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
        string capabilityKey,
        string approvedActionId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}
