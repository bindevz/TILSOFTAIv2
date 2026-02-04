namespace TILSOFTAI.Domain.Errors;

/// <summary>
/// Centralized error code definitions with metadata.
/// </summary>
public static class ErrorCodes
{
    #region Authentication (401)
    
    public static readonly ErrorCodeDefinition AuthFailed = new(
        "AUTH_FAILED", 401, "Authentication failed", "error.auth.failed");
    
    public static readonly ErrorCodeDefinition AuthTokenExpired = new(
        "AUTH_TOKEN_EXPIRED", 401, "Token has expired", "error.auth.token_expired");
    
    public static readonly ErrorCodeDefinition AuthTokenInvalid = new(
        "AUTH_TOKEN_INVALID", 401, "Token is invalid", "error.auth.token_invalid");
    
    #endregion

    #region Authorization (403)
    
    public static readonly ErrorCodeDefinition AuthzForbidden = new(
        "AUTHZ_FORBIDDEN", 403, "Access denied", "error.authz.forbidden");
    
    public static readonly ErrorCodeDefinition AuthzRoleRequired = new(
        "AUTHZ_ROLE_REQUIRED", 403, "Required role missing", "error.authz.role_required");
    
    public static readonly ErrorCodeDefinition AuthzTenantMismatch = new(
        "AUTHZ_TENANT_MISMATCH", 403, "Tenant access denied", "error.authz.tenant_mismatch");
    
    #endregion

    #region Validation (400)
    
    public static readonly ErrorCodeDefinition ValidationInputTooLong = new(
        "VALIDATION_INPUT_TOO_LONG", 400, "Input exceeds maximum length", "error.validation.input_too_long");
    
    public static readonly ErrorCodeDefinition ValidationInvalidJson = new(
        "VALIDATION_INVALID_JSON", 400, "Invalid JSON format", "error.validation.invalid_json");
    
    public static readonly ErrorCodeDefinition ValidationRequiredField = new(
        "VALIDATION_REQUIRED_FIELD", 400, "Required field missing", "error.validation.required_field");
    
    public static readonly ErrorCodeDefinition ValidationPromptInjection = new(
        "VALIDATION_PROMPT_INJECTION", 400, "Potential prompt injection detected", "error.validation.prompt_injection");
    
    public static readonly ErrorCodeDefinition ValidationForbiddenPattern = new(
        "VALIDATION_FORBIDDEN_PATTERN", 400, "Input contains forbidden pattern", "error.validation.forbidden_pattern");
    
    #endregion

    #region Tool (400)
    
    public static readonly ErrorCodeDefinition ToolNotFound = new(
        "TOOL_NOT_FOUND", 400, "Tool not found", "error.tool.not_found");
    
    public static readonly ErrorCodeDefinition ToolDisabled = new(
        "TOOL_DISABLED", 400, "Tool is disabled", "error.tool.disabled");
    
    public static readonly ErrorCodeDefinition ToolArgsInvalid = new(
        "TOOL_ARGS_INVALID", 400, "Tool arguments invalid", "error.tool.args_invalid");
    
    public static readonly ErrorCodeDefinition ToolExecutionFailed = new(
        "TOOL_EXECUTION_FAILED", 500, "Tool execution failed", "error.tool.execution_failed");
    
    public static readonly ErrorCodeDefinition ToolValidationFailed = new(
        "TOOL_VALIDATION_FAILED", 400, "Tool validation failed", "error.tool.validation_failed");
    
    #endregion

    #region Chat (500)
    
    public static readonly ErrorCodeDefinition ChatFailed = new(
        "CHAT_FAILED", 500, "Chat processing failed", "error.chat.failed");
    
    public static readonly ErrorCodeDefinition ChatMaxStepsExceeded = new(
        "CHAT_MAX_STEPS_EXCEEDED", 400, "Maximum chat steps exceeded", "error.chat.max_steps");
    
    public static readonly ErrorCodeDefinition ChatMaxToolsExceeded = new(
        "CHAT_MAX_TOOLS_EXCEEDED", 400, "Maximum tool calls exceeded", "error.chat.max_tools");
    
    #endregion

    #region System (500)
    
    public static readonly ErrorCodeDefinition SysSqlFailed = new(
        "SYS_SQL_FAILED", 500, "Database operation failed", "error.sys.sql_failed");
    
    public static readonly ErrorCodeDefinition SysSqlTimeout = new(
        "SYS_SQL_TIMEOUT", 504, "Database operation timed out", "error.sys.sql_timeout");
    
    public static readonly ErrorCodeDefinition SysLlmFailed = new(
        "SYS_LLM_FAILED", 502, "LLM service unavailable", "error.sys.llm_failed");
    
    public static readonly ErrorCodeDefinition SysCircuitOpen = new(
        "SYS_CIRCUIT_OPEN", 503, "Service circuit breaker open", "error.sys.circuit_open");
    
    #endregion

    #region Rate Limit (429)
    
    public static readonly ErrorCodeDefinition RateLimitExceeded = new(
        "RATE_LIMIT_EXCEEDED", 429, "Rate limit exceeded", "error.rate.limit_exceeded");
    
    public static readonly ErrorCodeDefinition RateQuotaExceeded = new(
        "RATE_QUOTA_EXCEEDED", 429, "Quota exceeded", "error.rate.quota_exceeded");
    
    #endregion

    #region Request (400/413)
    
    public static readonly ErrorCodeDefinition RequestTooLarge = new(
        "REQUEST_TOO_LARGE", 413, "Request body too large", "error.request.too_large");
    
    public static readonly ErrorCodeDefinition RequestInvalid = new(
        "REQUEST_INVALID", 400, "Invalid request format", "error.request.invalid");
    
    #endregion

    /// <summary>
    /// Registry of all error codes for lookup.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ErrorCodeDefinition> Registry = 
        new Dictionary<string, ErrorCodeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [AuthFailed.Code] = AuthFailed,
            [AuthTokenExpired.Code] = AuthTokenExpired,
            [AuthTokenInvalid.Code] = AuthTokenInvalid,
            [AuthzForbidden.Code] = AuthzForbidden,
            [AuthzRoleRequired.Code] = AuthzRoleRequired,
            [AuthzTenantMismatch.Code] = AuthzTenantMismatch,
            [ValidationInputTooLong.Code] = ValidationInputTooLong,
            [ValidationInvalidJson.Code] = ValidationInvalidJson,
            [ValidationRequiredField.Code] = ValidationRequiredField,
            [ValidationPromptInjection.Code] = ValidationPromptInjection,
            [ValidationForbiddenPattern.Code] = ValidationForbiddenPattern,
            [ToolNotFound.Code] = ToolNotFound,
            [ToolDisabled.Code] = ToolDisabled,
            [ToolArgsInvalid.Code] = ToolArgsInvalid,
            [ToolExecutionFailed.Code] = ToolExecutionFailed,
            [ToolValidationFailed.Code] = ToolValidationFailed,
            [ChatFailed.Code] = ChatFailed,
            [ChatMaxStepsExceeded.Code] = ChatMaxStepsExceeded,
            [ChatMaxToolsExceeded.Code] = ChatMaxToolsExceeded,
            [SysSqlFailed.Code] = SysSqlFailed,
            [SysSqlTimeout.Code] = SysSqlTimeout,
            [SysLlmFailed.Code] = SysLlmFailed,
            [SysCircuitOpen.Code] = SysCircuitOpen,
            [RateLimitExceeded.Code] = RateLimitExceeded,
            [RateQuotaExceeded.Code] = RateQuotaExceeded,
            [RequestTooLarge.Code] = RequestTooLarge,
            [RequestInvalid.Code] = RequestInvalid,
        };

    /// <summary>
    /// Gets error code definition by code string.
    /// </summary>
    public static ErrorCodeDefinition? GetByCode(string code)
    {
        return Registry.TryGetValue(code, out var definition) ? definition : null;
    }
}

/// <summary>
/// Definition of an error code with metadata.
/// </summary>
public sealed record ErrorCodeDefinition(
    string Code,
    int HttpStatus,
    string DefaultMessage,
    string LocalizationKey)
{
    /// <summary>
    /// Creates a TilsoftApiException from this error code.
    /// </summary>
    public TilsoftApiException ToException(string? message = null, object? detail = null)
        => new(Code, HttpStatus, message ?? DefaultMessage, detail);

    /// <summary>
    /// Creates an ErrorEnvelope from this error code.
    /// </summary>
    public ErrorEnvelope ToEnvelope(string? message = null, object? detail = null)
        => new()
        {
            Code = Code,
            Message = message ?? DefaultMessage,
            Detail = detail
        };
}
