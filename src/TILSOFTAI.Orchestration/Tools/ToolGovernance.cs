using TILSOFTAI.Domain.Errors;
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
            return ToolValidationResult.Fail(
                ToolValidationLocalizer.ToolNotEnabled(call.Name, language),
                ErrorCode.ToolValidationFailed);
        }

        if (tool.RequiredRoles.Length > 0)
        {
            var userRoles = new HashSet<string>(context.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!tool.RequiredRoles.All(role => userRoles.Contains(role)))
            {
                return ToolValidationResult.Fail(
                    ToolValidationLocalizer.ToolRequiresRoles(call.Name, tool.RequiredRoles, language),
                    ErrorCode.ToolValidationFailed);
            }
        }

        if (!string.IsNullOrWhiteSpace(tool.SpName)
            && !tool.SpName.StartsWith("ai_", StringComparison.OrdinalIgnoreCase))
        {
            return ToolValidationResult.Fail(
                ToolValidationLocalizer.ToolInvalidSpName(call.Name, language),
                ErrorCode.ToolValidationFailed);
        }

        var schemaValidation = _schemaValidator.Validate(tool.JsonSchema, call.ArgumentsJson);
        if (!schemaValidation.IsValid)
        {
            var errors = schemaValidation.Errors.Count > 0
                ? schemaValidation.Errors
                : string.IsNullOrWhiteSpace(schemaValidation.Summary)
                    ? Array.Empty<string>()
                    : new[] { schemaValidation.Summary };
            var summary = string.IsNullOrWhiteSpace(schemaValidation.Summary)
                ? "Schema validation failed."
                : schemaValidation.Summary;
            return ToolValidationResult.Fail(
                ToolValidationLocalizer.ToolSchemaInvalid(summary, language),
                ErrorCode.ToolArgsInvalid,
                errors);
        }

        return ToolValidationResult.Success(tool);
    }
}

public sealed record ToolValidationResult(
    bool IsValid,
    ToolDefinition? Tool,
    string? Error,
    string? Code,
    object? Detail)
{
    public static ToolValidationResult Success(ToolDefinition tool) => new(true, tool, null, null, null);
    public static ToolValidationResult Fail(string error, string? code = null, object? detail = null)
        => new(false, null, error, code, detail);
}
