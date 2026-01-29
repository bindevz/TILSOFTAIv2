using System.Text.Json;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Atomic;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Platform.Tools;

public sealed class AtomicExecutePlanToolHandler : IToolHandler
{
    private readonly AtomicDataEngine _atomicDataEngine;

    public AtomicExecutePlanToolHandler(AtomicDataEngine atomicDataEngine)
    {
        _atomicDataEngine = atomicDataEngine ?? throw new ArgumentNullException(nameof(atomicDataEngine));
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
        if (!argsDoc.RootElement.TryGetProperty("plan", out var planElement))
        {
            throw new InvalidOperationException("Missing required 'plan' argument.");
        }

        var planJson = planElement.GetRawText();

        // Execute plan via AtomicDataEngine (which validates and optimizes internally)
        var resultDoc = await _atomicDataEngine.ExecuteAsync(planJson, context, cancellationToken);

        return resultDoc.RootElement.GetRawText();
    }
}
