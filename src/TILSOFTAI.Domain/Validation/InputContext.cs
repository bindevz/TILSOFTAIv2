namespace TILSOFTAI.Domain.Validation;

/// <summary>
/// Defines the type of input being validated, used to apply context-specific validation rules.
/// </summary>
public enum InputContextType
{
    /// <summary>
    /// User chat message input.
    /// </summary>
    ChatMessage = 0,

    /// <summary>
    /// Tool argument input.
    /// </summary>
    ToolArgument = 1,

    /// <summary>
    /// HTTP header value.
    /// </summary>
    Header = 2,

    /// <summary>
    /// Query parameter value.
    /// </summary>
    QueryParam = 3
}

/// <summary>
/// Provides context for input validation including rules and constraints.
/// </summary>
public sealed class InputContext
{
    /// <summary>
    /// The type of input being validated.
    /// </summary>
    public InputContextType Type { get; init; } = InputContextType.ChatMessage;

    /// <summary>
    /// Maximum allowed length for this input type. If null, uses global default.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Optional field name for error reporting.
    /// </summary>
    public string? FieldName { get; init; }

    /// <summary>
    /// Whether to require Unicode normalization (NFC).
    /// </summary>
    public bool RequireUnicodeNormalization { get; init; } = true;

    /// <summary>
    /// Whether to enable prompt injection detection for this context.
    /// </summary>
    public bool EnablePromptInjectionDetection { get; init; } = true;

    /// <summary>
    /// Creates a context for chat message validation.
    /// </summary>
    public static InputContext ForChatMessage(int? maxLength = null) => new()
    {
        Type = InputContextType.ChatMessage,
        MaxLength = maxLength,
        FieldName = "input",
        EnablePromptInjectionDetection = true
    };

    /// <summary>
    /// Creates a context for tool argument validation.
    /// </summary>
    public static InputContext ForToolArgument(string? fieldName = null, int? maxLength = null) => new()
    {
        Type = InputContextType.ToolArgument,
        MaxLength = maxLength,
        FieldName = fieldName ?? "arguments",
        EnablePromptInjectionDetection = false
    };

    /// <summary>
    /// Creates a context for header validation.
    /// </summary>
    public static InputContext ForHeader(string headerName) => new()
    {
        Type = InputContextType.Header,
        MaxLength = 8192,
        FieldName = headerName,
        EnablePromptInjectionDetection = false,
        RequireUnicodeNormalization = false
    };

    /// <summary>
    /// Creates a context for query parameter validation.
    /// </summary>
    public static InputContext ForQueryParam(string paramName) => new()
    {
        Type = InputContextType.QueryParam,
        MaxLength = 2048,
        FieldName = paramName,
        EnablePromptInjectionDetection = false
    };
}
