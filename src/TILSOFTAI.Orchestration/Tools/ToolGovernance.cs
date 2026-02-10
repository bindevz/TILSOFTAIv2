using System.Diagnostics;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Domain.Validation;
using TILSOFTAI.Orchestration.Llm;

namespace TILSOFTAI.Orchestration.Tools;

public sealed class ToolGovernance
{
    private readonly IJsonSchemaValidator _schemaValidator;
    private readonly IInputValidator _inputValidator;
    private readonly IAuditLogger _auditLogger;
    private readonly IMetricsService _metrics;

    public ToolGovernance(
        IJsonSchemaValidator schemaValidator,
        IInputValidator inputValidator,
        IAuditLogger auditLogger,
        IMetricsService metrics)
    {
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public ToolValidationResult Validate(
        LlmToolCall call,
        IReadOnlyDictionary<string, ToolDefinition> allowlist,
        TilsoftExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        var language = context.Language;

        if (!allowlist.TryGetValue(call.Name, out var tool))
        {
            sw.Stop();
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

            // PATCH 31.06: Governance audit event + metrics
            _auditLogger.LogGovernanceEvent(GovernanceAuditEvent.Denied(
                context.TenantId, context.UserId, context.CorrelationId,
                call.Name, "tool_governance",
                (context.Roles ?? Array.Empty<string>()).ToArray(),
                Array.Empty<string>(),
                "Tool not in allowlist", ErrorCode.ToolNotFound,
                sw.ElapsedMilliseconds));
            _metrics.IncrementCounter(MetricNames.GovernanceDenyTotal);

            return ToolValidationResult.Fail(
                ToolValidationLocalizer.ToolNotEnabled(call.Name, language),
                ErrorCode.ToolNotFound);
        }

        if (tool.RequiredRoles.Length > 0)
        {
            var userRoles = new HashSet<string>(context.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!tool.RequiredRoles.All(role => userRoles.Contains(role)))
            {
                sw.Stop();
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

                // PATCH 31.06: Governance audit event + metrics
                _auditLogger.LogGovernanceEvent(GovernanceAuditEvent.Denied(
                    context.TenantId, context.UserId, context.CorrelationId,
                    call.Name, "tool_governance",
                    (context.Roles ?? Array.Empty<string>()).ToArray(),
                    tool.RequiredRoles,
                    "Missing required roles", ErrorCode.ToolValidationFailed,
                    sw.ElapsedMilliseconds));
                _metrics.IncrementCounter(MetricNames.GovernanceDenyTotal);

                return ToolValidationResult.Fail(
                    ToolValidationLocalizer.ToolRequiresRoles(call.Name, tool.RequiredRoles, language),
                    ErrorCode.ToolValidationFailed);
            }
        }

        if (!string.IsNullOrWhiteSpace(tool.SpName)
            && !tool.SpName.StartsWith("ai_", StringComparison.OrdinalIgnoreCase))
        {
            sw.Stop();
            // PATCH 31.06: Governance audit event + metrics
            _auditLogger.LogGovernanceEvent(GovernanceAuditEvent.Denied(
                context.TenantId, context.UserId, context.CorrelationId,
                call.Name, "tool_governance",
                (context.Roles ?? Array.Empty<string>()).ToArray(),
                tool.RequiredRoles,
                "Invalid SP name prefix", ErrorCode.ToolValidationFailed,
                sw.ElapsedMilliseconds));
            _metrics.IncrementCounter(MetricNames.GovernanceDenyTotal);

            return ToolValidationResult.Fail(
                ToolValidationLocalizer.ToolInvalidSpName(call.Name, language),
                ErrorCode.ToolValidationFailed);
        }

        // Validate and sanitize tool arguments
        var inputValidation = _inputValidator.ValidateToolArguments(call.ArgumentsJson, call.Name);
        if (!inputValidation.IsValid)
        {
            sw.Stop();
            var firstError = inputValidation.Errors.FirstOrDefault();
            // PATCH 31.06: Governance audit event + metrics
            _auditLogger.LogGovernanceEvent(GovernanceAuditEvent.Denied(
                context.TenantId, context.UserId, context.CorrelationId,
                call.Name, "tool_governance",
                (context.Roles ?? Array.Empty<string>()).ToArray(),
                tool.RequiredRoles,
                "Input validation failed", firstError?.Code ?? ErrorCode.InvalidInput,
                sw.ElapsedMilliseconds));
            _metrics.IncrementCounter(MetricNames.GovernanceDenyTotal);

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
            sw.Stop();
            var errors = schemaValidation.Errors.Count > 0
                ? schemaValidation.Errors
                : string.IsNullOrWhiteSpace(schemaValidation.Summary)
                    ? Array.Empty<string>()
                    : new[] { schemaValidation.Summary };
            var summary = string.IsNullOrWhiteSpace(schemaValidation.Summary)
                ? "Schema validation failed."
                : schemaValidation.Summary;

            // PATCH 31.06: Governance audit event + metrics
            _auditLogger.LogGovernanceEvent(GovernanceAuditEvent.Denied(
                context.TenantId, context.UserId, context.CorrelationId,
                call.Name, "tool_governance",
                (context.Roles ?? Array.Empty<string>()).ToArray(),
                tool.RequiredRoles,
                "Schema validation failed", ErrorCode.ToolArgsInvalid,
                sw.ElapsedMilliseconds));
            _metrics.IncrementCounter(MetricNames.GovernanceDenyTotal);

            return ToolValidationResult.Fail(
                ToolValidationLocalizer.ToolSchemaInvalid(summary, language),
                ErrorCode.ToolArgsInvalid,
                errors);
        }

        sw.Stop();
        // PATCH 31.06: Governance audit event for success
        _auditLogger.LogGovernanceEvent(GovernanceAuditEvent.Allowed(
            context.TenantId, context.UserId, context.CorrelationId,
            call.Name, "tool_governance",
            (context.Roles ?? Array.Empty<string>()).ToArray(),
            tool.RequiredRoles,
            sw.ElapsedMilliseconds));
        _metrics.IncrementCounter(MetricNames.GovernanceAllowTotal);

        return ToolValidationResult.Success(tool, sanitizedArgs);
    }

    /// <summary>
    /// PATCH 31.01: Unified governance + execution.
    /// Validates RBAC, schema, input sanitization, then executes.
    /// Used by BOTH LLM tool-calling loop AND deterministic orchestrator.
    /// </summary>
    public async Task<GovernedExecutionResult> ValidateAndExecuteAsync(
        string toolName,
        string argumentsJson,
        IReadOnlyDictionary<string, ToolDefinition> toolAllowlist,
        TilsoftExecutionContext context,
        IToolHandler toolHandler,
        CancellationToken ct)
    {
        // Build a synthetic LlmToolCall for validation
        var call = new LlmToolCall
        {
            Name = toolName,
            ArgumentsJson = argumentsJson
        };

        var validation = Validate(call, toolAllowlist, context);

        if (!validation.IsValid)
        {
            _auditLogger.LogAuthorizationEvent(AuthzAuditEvent.Denied(
                context.TenantId,
                context.UserId,
                context.CorrelationId,
                context.IpAddress,
                context.UserAgent,
                $"tool:{toolName}",
                "execute",
                context.Roles ?? Array.Empty<string>(),
                validation.Tool?.RequiredRoles ?? Array.Empty<string>(),
                "ToolGovernance.ValidateAndExecute"));

            return GovernedExecutionResult.Denied(
                validation.Error ?? "Governance validation failed.",
                validation.Code);
        }

        // Use sanitized arguments from validation
        var sanitizedArgs = validation.SanitizedArgumentsJson ?? argumentsJson;
        var result = await toolHandler.ExecuteAsync(
            validation.Tool!, sanitizedArgs, context, ct);

        return GovernedExecutionResult.Success(result ?? "{}");
    }
}

/// <summary>
/// PATCH 31.01: Result of governed tool execution.
/// </summary>
public sealed record GovernedExecutionResult(
    bool IsAllowed,
    string? Result,
    string? DenialReason,
    string? DenialCode)
{
    public static GovernedExecutionResult Success(string result)
        => new(true, result, null, null);
    public static GovernedExecutionResult Denied(string reason, string? code = null)
        => new(false, null, reason, code);
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

