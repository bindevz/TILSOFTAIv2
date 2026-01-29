using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Llm;

namespace TILSOFTAI.Orchestration.Tools;

public sealed class ToolGovernance
{
    private readonly IJsonSchemaValidator _schemaValidator;

    public ToolGovernance(IJsonSchemaValidator schemaValidator)
    {
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
    }

    public ToolValidationResult Validate(
        LlmToolCall call,
        IReadOnlyDictionary<string, ToolDefinition> allowlist,
        TilsoftExecutionContext context)
    {
        var language = context.Language;

        if (!allowlist.TryGetValue(call.Name, out var tool))
        {
            return ToolValidationResult.Fail(ToolValidationLocalizer.ToolNotEnabled(call.Name, language));
        }

        if (tool.RequiredRoles.Length > 0)
        {
            var userRoles = new HashSet<string>(context.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!tool.RequiredRoles.All(role => userRoles.Contains(role)))
            {
                return ToolValidationResult.Fail(
                    ToolValidationLocalizer.ToolRequiresRoles(call.Name, tool.RequiredRoles, language));
            }
        }

        if (!string.IsNullOrWhiteSpace(tool.SpName)
            && !tool.SpName.StartsWith("ai_", StringComparison.OrdinalIgnoreCase))
        {
            return ToolValidationResult.Fail(ToolValidationLocalizer.ToolInvalidSpName(call.Name, language));
        }

        var schemaValidation = _schemaValidator.Validate(tool.JsonSchema, call.ArgumentsJson);
        if (!schemaValidation.IsValid)
        {
            var errorDetail = string.IsNullOrWhiteSpace(schemaValidation.Error)
                ? "Schema validation failed."
                : schemaValidation.Error;
            return ToolValidationResult.Fail(ToolValidationLocalizer.ToolSchemaInvalid(errorDetail, language));
        }

        return ToolValidationResult.Success(tool);
    }
}

public sealed record ToolValidationResult(bool IsValid, ToolDefinition? Tool, string? Error)
{
    public static ToolValidationResult Success(ToolDefinition tool) => new(true, tool, null);
    public static ToolValidationResult Fail(string error) => new(false, null, error);
}
