using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Domain.Errors;

public interface ISqlErrorLogWriter
{
    Task WriteAsync(TilsoftExecutionContext context, string code, string message, object? detail, CancellationToken ct);
}
