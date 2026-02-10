using System.Text.Json;

namespace TILSOFTAI.Orchestration.Policies;

/// <summary>
/// Immutable snapshot of resolved runtime policies for a request scope.
/// Attached per-request to avoid re-querying per step.
/// </summary>
public sealed class RuntimePolicySnapshot
{
    private readonly IReadOnlyDictionary<string, JsonElement> _policies;

    public RuntimePolicySnapshot(IReadOnlyDictionary<string, JsonElement> policies)
    {
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
    }

    public static RuntimePolicySnapshot Empty { get; } =
        new(new Dictionary<string, JsonElement>());

    /// <summary>
    /// Try to get a parsed policy by key.
    /// </summary>
    public bool TryGetPolicy(string policyKey, out JsonElement value)
    {
        return _policies.TryGetValue(policyKey, out value);
    }

    /// <summary>
    /// Get a typed value from a policy, with fallback.
    /// </summary>
    public T GetValueOrDefault<T>(string policyKey, string propertyName, T fallback)
    {
        if (!_policies.TryGetValue(policyKey, out var element))
            return fallback;

        if (element.ValueKind != JsonValueKind.Object)
            return fallback;

        if (!element.TryGetProperty(propertyName, out var prop))
            return fallback;

        try
        {
            return prop.Deserialize<T>() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Check if a policy is enabled (convention: policy JSON has "enabled" bool).
    /// </summary>
    public bool IsEnabled(string policyKey)
    {
        return GetValueOrDefault(policyKey, "enabled", false);
    }

    public IReadOnlyDictionary<string, JsonElement> All => _policies;
}
