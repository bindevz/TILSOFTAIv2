using System.Text.Json;

namespace TILSOFTAI.Orchestration.Capabilities;

public static class CapabilityArgumentValidator
{
    public const string ValidationFailedCode = "CAPABILITY_ARGUMENT_VALIDATION_FAILED";

    public static CapabilityArgumentValidationResult Validate(
        CapabilityDescriptor capability,
        string argumentsJson)
    {
        ArgumentNullException.ThrowIfNull(capability);

        var contract = capability.ArgumentContract;
        if (contract is null
            || (contract.RequiredArguments.Count == 0
                && contract.AllowedArguments.Count == 0
                && contract.AllowAdditionalArguments))
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

            var actualNames = document.RootElement
                .EnumerateObject()
                .Select(property => NormalizeName(property.Name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        }

        return CapabilityArgumentValidationResult.Valid();
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
