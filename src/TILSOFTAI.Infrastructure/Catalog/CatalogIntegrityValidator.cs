using System.Text.Json;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Infrastructure.Catalog;

public static class CatalogIntegrityValidator
{
    private static readonly HashSet<string> RawSecretMetadataKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "authToken",
        "apiKey",
        "authorization",
        "password",
        "secret"
    };

    public static CatalogIntegrityValidationResult Validate(
        IReadOnlyList<CapabilityDescriptor> capabilities,
        IReadOnlyDictionary<string, ExternalConnectionOptions> connections)
    {
        var errors = new List<string>();

        foreach (var group in capabilities
            .Where(capability => !string.IsNullOrWhiteSpace(capability.CapabilityKey))
            .GroupBy(capability => capability.CapabilityKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            errors.Add($"duplicate_capability_key:{group.Key}");
        }

        foreach (var capability in capabilities)
        {
            ValidateCapability(capability, connections, errors);
        }

        foreach (var pair in connections)
        {
            ValidateConnection(pair.Key, pair.Value, errors);
        }

        return errors.Count == 0
            ? CatalogIntegrityValidationResult.Valid()
            : CatalogIntegrityValidationResult.Invalid(errors);
    }

    public static CatalogIntegrityValidationResult ValidateCapabilityMutation(
        CapabilityDescriptor capability,
        IReadOnlyDictionary<string, ExternalConnectionOptions> connections)
    {
        var errors = new List<string>();
        ValidateCapability(capability, connections, errors);
        return errors.Count == 0
            ? CatalogIntegrityValidationResult.Valid()
            : CatalogIntegrityValidationResult.Invalid(errors);
    }

    public static CatalogIntegrityValidationResult ValidateConnectionMutation(
        string connectionName,
        ExternalConnectionOptions connection)
    {
        var errors = new List<string>();
        ValidateConnection(connectionName, connection, errors);
        return errors.Count == 0
            ? CatalogIntegrityValidationResult.Valid()
            : CatalogIntegrityValidationResult.Invalid(errors);
    }

    private static void ValidateCapability(
        CapabilityDescriptor capability,
        IReadOnlyDictionary<string, ExternalConnectionOptions> connections,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(capability.CapabilityKey))
        {
            errors.Add("capability_key_required");
        }

        if (string.IsNullOrWhiteSpace(capability.Domain))
        {
            errors.Add($"capability_domain_required:{capability.CapabilityKey}");
        }

        if (string.IsNullOrWhiteSpace(capability.AdapterType))
        {
            errors.Add($"capability_adapter_required:{capability.CapabilityKey}");
        }

        if (string.IsNullOrWhiteSpace(capability.Operation))
        {
            errors.Add($"capability_operation_required:{capability.CapabilityKey}");
        }

        foreach (var key in capability.IntegrationBinding.Keys)
        {
            if (RawSecretMetadataKeys.Contains(key))
            {
                errors.Add($"capability_raw_secret_metadata:{capability.CapabilityKey}:{key}");
            }
        }

        if (string.Equals(capability.AdapterType, "rest-json", StringComparison.OrdinalIgnoreCase))
        {
            if (!capability.IntegrationBinding.TryGetValue("connectionName", out var connectionName)
                || string.IsNullOrWhiteSpace(connectionName))
            {
                errors.Add($"capability_rest_connection_required:{capability.CapabilityKey}");
            }
            else if (!connections.ContainsKey(connectionName))
            {
                errors.Add($"capability_rest_connection_unresolved:{capability.CapabilityKey}:{connectionName}");
            }
        }

        ValidateContract(capability, errors);
    }

    private static void ValidateConnection(string connectionName, ExternalConnectionOptions connection, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            errors.Add("connection_name_required");
        }

        if (string.IsNullOrWhiteSpace(connection.BaseUrl))
        {
            errors.Add($"connection_base_url_required:{connectionName}");
        }

        if (!string.IsNullOrWhiteSpace(connection.AuthScheme)
            && string.IsNullOrWhiteSpace(connection.AuthTokenSecret))
        {
            errors.Add($"connection_auth_secret_required:{connectionName}");
        }

        if (!string.IsNullOrWhiteSpace(connection.ApiKeyHeader)
            && string.IsNullOrWhiteSpace(connection.ApiKeySecret))
        {
            errors.Add($"connection_api_key_secret_required:{connectionName}");
        }

        if (connection.Headers.Keys.Any(RawSecretMetadataKeys.Contains))
        {
            errors.Add($"connection_raw_secret_header:{connectionName}");
        }
    }

    private static void ValidateContract(CapabilityDescriptor capability, List<string> errors)
    {
        var contract = capability.ArgumentContract;
        if (contract is null)
        {
            errors.Add($"capability_contract_required:{capability.CapabilityKey}");
            return;
        }

        var known = new HashSet<string>(
            contract.RequiredArguments.Concat(contract.AllowedArguments).Select(NormalizeName),
            StringComparer.OrdinalIgnoreCase);

        foreach (var rule in contract.Arguments)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                errors.Add($"capability_contract_rule_name_required:{capability.CapabilityKey}");
                continue;
            }

            known.Add(NormalizeName(rule.Name));

            if (string.IsNullOrWhiteSpace(rule.Type))
            {
                errors.Add($"capability_contract_rule_type_required:{capability.CapabilityKey}:{rule.Name}");
            }
        }

        foreach (var required in contract.RequiredArguments.Select(NormalizeName))
        {
            if (!known.Contains(required))
            {
                errors.Add($"capability_contract_required_argument_unknown:{capability.CapabilityKey}:{required}");
            }
        }
    }

    public static string SerializeErrors(IReadOnlyList<string> errors) =>
        JsonSerializer.Serialize(new { errors });

    private static string NormalizeName(string name) => name.Trim().TrimStart('@');
}

public sealed class CatalogIntegrityValidationResult
{
    private CatalogIntegrityValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }

    public static CatalogIntegrityValidationResult Valid() => new(true, Array.Empty<string>());
    public static CatalogIntegrityValidationResult Invalid(IReadOnlyList<string> errors) => new(false, errors);
}
