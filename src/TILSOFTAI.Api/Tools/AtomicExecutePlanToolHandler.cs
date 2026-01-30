using System.Text.Json;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Atomic;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Api.Tools;

public sealed class AtomicExecutePlanToolHandler : IToolHandler
{
    private const string AtomicStoredProcedure = "ai_atomic_execute_plan";
    private readonly IJsonSchemaValidator _schemaValidator;
    private readonly AtomicDataEngine _atomicDataEngine;

    public AtomicExecutePlanToolHandler(IJsonSchemaValidator schemaValidator, AtomicDataEngine atomicDataEngine)
    {
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _atomicDataEngine = atomicDataEngine ?? throw new ArgumentNullException(nameof(atomicDataEngine));
    }

    public async Task<string> ExecuteAsync(ToolDefinition tool, string argumentsJson, TilsoftExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(tool.SpName, AtomicStoredProcedure, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Atomic execute plan tool handler can only execute ai_atomic_execute_plan.");
        }

        var schemaValidation = _schemaValidator.Validate(tool.JsonSchema, argumentsJson);
        if (!schemaValidation.IsValid)
        {
            throw new InvalidOperationException(schemaValidation.Summary ?? "Atomic execute plan arguments failed schema validation.");
        }

        using var doc = JsonDocument.Parse(argumentsJson);
        if (!doc.RootElement.TryGetProperty("planJson", out var planNode))
        {
            throw new InvalidOperationException("atomic_execute_plan requires planJson.");
        }

        var planJson = planNode.ValueKind == JsonValueKind.String
            ? planNode.GetString() ?? string.Empty
            : planNode.GetRawText();

        if (string.IsNullOrWhiteSpace(planJson))
        {
            throw new InvalidOperationException("atomic_execute_plan requires planJson.");
        }

        using var result = await _atomicDataEngine.ExecuteAsync(planJson, context, cancellationToken);
        return result.RootElement.GetRawText();
    }
}
