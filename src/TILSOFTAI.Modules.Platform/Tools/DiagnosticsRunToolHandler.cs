using System.Text.Json;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Platform.Tools;

public sealed class DiagnosticsRunToolHandler : IToolHandler
{
    private readonly ISqlExecutor _sqlExecutor;

    public DiagnosticsRunToolHandler(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
    }

    public async Task<string> ExecuteAsync(
        ToolDefinition tool,
        string argumentsJson,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (tool is null)
        {
            throw new ArgumentNullException(nameof(tool));
        }

        using var argsDoc = JsonDocument.Parse(argumentsJson);
        var root = argsDoc.RootElement;

        if (!root.TryGetProperty("module", out var moduleElement) || moduleElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Missing required 'module' argument.");
        }

        if (!root.TryGetProperty("ruleKey", out var ruleKeyElement) || ruleKeyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Missing required 'ruleKey' argument.");
        }

        var module = moduleElement.GetString() ?? string.Empty;
        var ruleKey = ruleKeyElement.GetString() ?? string.Empty;
        var inputJson = root.TryGetProperty("inputJson", out var inputElement) && inputElement.ValueKind == JsonValueKind.String
            ? inputElement.GetString()
            : null;

        // Execute diagnostics via ISqlExecutor
        var result = await _sqlExecutor.ExecuteDiagnosticsAsync(
            "ai_diagnostics_run",
            context.TenantId,
            module,
            ruleKey,
            inputJson,
            cancellationToken);

        return result ?? "{}";
    }
}
