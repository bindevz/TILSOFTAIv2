namespace TILSOFTAI.Orchestration.Execution;

public interface ICompositeCapabilityExecutor
{
    Task<CapabilityExecutionEnvelope> ExecuteAsync(
        string compositeCapabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}
