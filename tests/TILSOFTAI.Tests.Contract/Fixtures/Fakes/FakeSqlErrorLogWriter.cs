using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Tests.Contract.Fixtures.Fakes;

/// <summary>
/// Fake ISqlErrorLogWriter for contract tests that does not persist errors.
/// </summary>
public sealed class FakeSqlErrorLogWriter : ISqlErrorLogWriter
{
    public Task WriteAsync(TilsoftExecutionContext context, string code, string message, object? detail, CancellationToken ct)
    {
        // No-op: do not persist error logs in tests
        return Task.CompletedTask;
    }
}
