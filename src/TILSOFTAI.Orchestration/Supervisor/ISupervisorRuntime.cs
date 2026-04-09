using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Supervisor;

public interface ISupervisorRuntime
{
    Task<SupervisorResult> RunAsync(SupervisorRequest request, TilsoftExecutionContext ctx, CancellationToken ct);

    IAsyncEnumerable<SupervisorStreamEvent> RunStreamAsync(
        SupervisorRequest request,
        TilsoftExecutionContext ctx,
        CancellationToken ct);
}
