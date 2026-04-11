using System.Text.Json;
using System.Text.RegularExpressions;

namespace TILSOFTAI.Orchestration.Capabilities;

public static class CapabilityArgumentValidator
{
    public const string ValidationFailedCode = "CAPABILITY_ARGUMENT_VALIDATION_FAILED";
    private static readonly Regex CurrencyCodeRegex = new("^[A-Z]{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ItemNumberRegex = new("^[A-Za-z0-9._-]{1,50}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex InvoiceNumberRegex = new("^[A-Za-z0-9._/-]{1,50}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TenantIdRegex = new("^[A-Za-z0-9._:-]{1,80}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CapabilityArgumentValidationResult Validate(
        CapabilityDescriptor capability,
        string argumentsJson)
    {
        ArgumentNullException.ThrowIfNull(capability);

        var contract = capability.ArgumentContract;
        if (contract is null
            || (contract.RequiredArguments.Count == 0
                && contract.AllowedArguments.Count == 0
                && contract.AllowAdditionalArguments
                && contract.Arguments.Count == 0))
        {
            return CapabilityArgumentValidationResult.Valid();
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        }
        catch (JsonException ex)
        {
            return CapabilityArgumentValidationResult.Invalid(new
            {
                capabilityKey = capability.CapabilityKey,
                reason = "invalid_json",
                message = ex.Message
            });
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return CapabilityArgumentValidationResult.Invalid(new
                {
                    capabilityKey = capability.CapabilityKey,
                    reason = "arguments_must_be_object"
                });
            }

            var properties = document.RootElement
                .EnumerateObject()
                .ToDictionary(property => NormalizeName(property.Name), property => property, StringComparer.OrdinalIgnoreCase);

            var actualNames = properties.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = contract.RequiredArguments
                .Select(NormalizeName)
                .Where(required => !actualNames.Contains(required))
                .ToArray();

            if (missing.Length > 0)
            {
                return CapabilityArgumentValidationResult.Invalid(new
                {
                    capabilityKey = capability.CapabilityKey,
                    reason = "missing_required_arguments",
                    missing
                });
            }

            if (!contract.AllowAdditionalArguments && contract.AllowedArguments.Count > 0)
            {
                var allowed = contract.AllowedArguments
                    .Select(NormalizeName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var required in contract.RequiredArguments.Select(NormalizeName))
                {
                    allowed.Add(required);
                }

                var extra = actualNames
                    .Where(actual => !allowed.Contains(actual))
                    .ToArray();

                if (extra.Length > 0)
                {
                    return CapabilityArgumentValidationResult.Invalid(new
                    {
                        capabilityKey = capability.CapabilityKey,
                        reason = "unexpected_arguments",
                        extra
                    });
                }
            }

            foreach (var rule in contract.Arguments)
            {
                if (string.IsNullOrWhiteSpace(rule.Name))
                {
                    continue;
                }

                var normalizedName = NormalizeName(rule.Name);
                if (!properties.TryGetValue(normalizedName, out var property))
                {
                    continue;
                }

                var ruleValidation = ValidateRule(capability.CapabilityKey, normalizedName, property.Value, rule);
                if (!ruleValidation.IsValid)
                {
                    return ruleValidation;
                }
            }
        }

        return CapabilityArgumentValidationResult.Valid();
    }

    private static CapabilityArgumentValidationResult ValidateRule(
        string capabilityKey,
        string argumentName,
        JsonElement value,
        CapabilityArgumentRule rule)
    {
        if (!ValidateType(value, rule.Type))
        {
            return CapabilityArgumentValidationResult.Invalid(new
            {
                capabilityKey,
                reason = "invalid_argument_type",
                argument = argumentName,
                expectedType = rule.Type,
                actualType = value.ValueKind.ToString()
            });
        }

        if (rule.Enum.Count > 0)
        {
            var actual = ValueAsString(value);
            if (!rule.Enum.Any(allowed => string.Equals(allowed, actual, StringComparison.OrdinalIgnoreCase)))
            {
                return CapabilityArgumentValidationResult.Invalid(new
                {
                    capabilityKey,
                    reason = "invalid_argument_enum",
                    argument = argumentName,
                    allowed = rule.Enum
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(rule.Format) && !ValidateFormat(value, rule.Format))
        {
            return CapabilityArgumentValidationResult.Invalid(new
            {
                capabilityKey,
                reason = "invalid_argument_format",
                argument = argumentName,
                format = rule.Format
            });
        }

        if (!string.IsNullOrWhiteSpace(rule.Pattern))
        {
            var actual = ValueAsString(value);
            if (actual is null || !Regex.IsMatch(actual, rule.Pattern, RegexOptions.CultureInvariant))
            {
                return CapabilityArgumentValidationResult.Invalid(new
                {
                    capabilityKey,
                    reason = "invalid_argument_pattern",
                    argument = argumentName
                });
            }
        }

        if (rule.MinLength is not null || rule.MaxLength is not null)
        {
            var actual = ValueAsString(value);
            if (actual is null)
            {
                return CapabilityArgumentValidationResult.Invalid(new
                {
                    capabilityKey,
                    reason = "invalid_argument_type",
                    argument = argumentName,
                    expectedType = "string",
                    actualType = value.ValueKind.ToString()
                });
            }

            if (rule.MinLength is not null && actual.Length < rule.MinLength.Value)
            {
                return CapabilityArgumentValidationResult.Invalid(new
                {
                    capabilityKey,
                    reason = "invalid_argument_min_length",
                    argument = argumentName,
                    minLength = rule.MinLength.Value
                });
            }

            if (rule.MaxLength is not null && actual.Length > rule.MaxLength.Value)
            {
                return CapabilityArgumentValidationResult.Invalid(new
                {
                    capabilityKey,
                    reason = "invalid_argument_max_length",
                    argument = argumentName,
                    maxLength = rule.MaxLength.Value
                });
            }
        }

        if (rule.Min is not null || rule.Max is not null)
        {
            if (!value.TryGetDecimal(out var numericValue))
            {
                return CapabilityArgumentValidationResult.Invalid(new
                {
                    capabilityKey,
                    reason = "invalid_argument_type",
                    argument = argumentName,
                    expectedType = "number",
                    actualType = value.ValueKind.ToString()
                });
            }

            if (rule.Min is not null && numericValue < rule.Min.Value)
            {
                return CapabilityArgumentValidationResult.Invalid(new
                {
                    capabilityKey,
                    reason = "invalid_argument_min",
                    argument = argumentName,
                    min = rule.Min.Value
                });
            }

            if (rule.Max is not null && numericValue > rule.Max.Value)
            {
                return CapabilityArgumentValidationResult.Invalid(new
                {
                    capabilityKey,
                    reason = "invalid_argument_max",
                    argument = argumentName,
                    max = rule.Max.Value
                });
            }
        }

        return CapabilityArgumentValidationResult.Valid();
    }

    private static bool ValidateType(JsonElement value, string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return true;
        }

        return type.Trim().ToLowerInvariant() switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => true
        };
    }

    private static bool ValidateFormat(JsonElement value, string format)
    {
        var actual = ValueAsString(value);
        if (actual is null)
        {
            return false;
        }

        return format.Trim().ToLowerInvariant() switch
        {
            "non-empty" => !string.IsNullOrWhiteSpace(actual),
            "currency-code" => CurrencyCodeRegex.IsMatch(actual),
            "item-number" => ItemNumberRegex.IsMatch(actual),
            "invoice-number" => InvoiceNumberRegex.IsMatch(actual),
            "tenant-id" => TenantIdRegex.IsMatch(actual),
            _ => true
        };
    }

    private static string? ValueAsString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string NormalizeName(string name) => name.Trim().TrimStart('@');
}

public sealed class CapabilityArgumentValidationResult
{
    private CapabilityArgumentValidationResult(bool isValid, object? detail)
    {
        IsValid = isValid;
        Detail = detail;
    }

    public bool IsValid { get; }
    public object? Detail { get; }

    public static CapabilityArgumentValidationResult Valid() => new(true, null);
    public static CapabilityArgumentValidationResult Invalid(object detail) => new(false, detail);
}
