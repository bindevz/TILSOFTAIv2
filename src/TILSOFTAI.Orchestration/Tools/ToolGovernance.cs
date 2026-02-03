using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Validation;
using TILSOFTAI.Orchestration.Llm;

namespace TILSOFTAI.Orchestration.Tools;

public sealed class ToolGovernance
{
    private readonly IJsonSchemaValidator _schemaValidator;
    private readonly IInputValidator _inputValidator;
    private readonly IAuditLogger _auditLogger;

    public ToolGovernance(
        IJsonSchemaValidator schemaValidator,
        IInputValidator inputValidator,
        IAuditLogger auditLogger)
    {
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public ToolValidationResult Validate(
        LlmToolCall call,
        IReadOnlyDictionary<string, ToolDefinition> allowlist,
        TilsoftExecutionContext context)
    {
        var language = context.Language;

        if (!allowlist.TryGetValue(call.Name, out var tool))
        {
            // Log authorization denied for unknown tool
            _auditLogger.LogAuthorizationEvent(AuthzAuditEvent.Denied(
                context.TenantId,
                context.UserId,
                context.CorrelationId,
                context.IpAddress,
                context.UserAgent,
                $"tool:{call.Name}",
                "execute",
                context.Roles ?? Array.Empty<string>(),
                Array.Empty<string>(),
                "ToolAllowlist"));

            return ToolValidationResult.Fail(
                ToolValidationLocalizer.ToolNotEnabled(call.Name, language),
                ErrorCode.ToolValidationFailed);
        }

        if (tool.RequiredRoles.Length > 0)
        {
            var userRoles = new HashSet<string>(context.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!tool.RequiredRoles.All(role => userRoles.Contains(role)))
            {
                // Log authorization denied for role mismatch
                _auditLogger.LogAuthorizationEvent(AuthzAuditEvent.Denied(
                    context.TenantId,
                    context.UserId,
                    context.CorrelationId,
                    context.IpAddress,
                    context.UserAgent,
                    $"tool:{call.Name}",
                    "execute",
                    context.Roles ?? Array.Empty<string>(),
                    tool.RequiredRoles,
                    "ToolRoleRequirement"));

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

        // Validate and sanitize tool arguments
        var inputValidation = _inputValidator.ValidateToolArguments(call.ArgumentsJson, call.Name);
        if (!inputValidation.IsValid)
        {
            var firstError = inputValidation.Errors.FirstOrDefault();
            return ToolValidationResult.Fail(
                firstError?.Message ?? "Tool arguments validation failed.",
                firstError?.Code ?? ErrorCode.InvalidInput,
                inputValidation.Errors.Select(e => e.Message).ToArray());
        }

        // Use sanitized arguments for schema validation
        var sanitizedArgs = inputValidation.SanitizedValue ?? call.ArgumentsJson;

        var schemaValidation = _schemaValidator.Validate(tool.JsonSchema, sanitizedArgs);
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

        return ToolValidationResult.Success(tool, sanitizedArgs);
    }
}

public sealed record ToolValidationResult(
    bool IsValid,
    ToolDefinition? Tool,
    string? Error,
    string? Code,
    object? Detail,
    string? SanitizedArgumentsJson = null)
{
    public static ToolValidationResult Success(ToolDefinition tool, string? sanitizedArgs = null)
        => new(true, tool, null, null, null, sanitizedArgs);
    public static ToolValidationResult Fail(string error, string? code = null, object? detail = null)
        => new(false, null, error, code, detail, null);
}
