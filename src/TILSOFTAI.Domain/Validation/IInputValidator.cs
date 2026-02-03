using System.Text.Json;

namespace TILSOFTAI.Domain.Validation;

/// <summary>
/// Interface for centralized input validation and sanitization.
/// </summary>
public interface IInputValidator
{
    /// <summary>
    /// Validates and sanitizes user input based on the provided context.
    /// </summary>
    /// <param name="input">The input string to validate.</param>
    /// <param name="context">The validation context containing rules and constraints.</param>
    /// <returns>Validation result with sanitized value or errors.</returns>
    ValidationResult ValidateUserInput(string? input, InputContext context);

    /// <summary>
    /// Validates tool arguments against the expected schema.
    /// </summary>
    /// <param name="argumentsJson">The JSON string containing tool arguments.</param>
    /// <param name="toolName">The name of the tool for error context.</param>
    /// <returns>Validation result with sanitized JSON or errors.</returns>
    ValidationResult ValidateToolArguments(string? argumentsJson, string toolName);

    /// <summary>
    /// Validates tool arguments from a JsonElement.
    /// </summary>
    /// <param name="arguments">The JsonElement containing tool arguments.</param>
    /// <param name="toolName">The name of the tool for error context.</param>
    /// <returns>Validation result with sanitized JSON or errors.</returns>
    ValidationResult ValidateToolArguments(JsonElement arguments, string toolName);
}
