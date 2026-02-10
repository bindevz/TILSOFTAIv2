namespace TILSOFTAI.Domain.Configuration;

/// <summary>
/// System-level configuration for RuntimePolicy provider.
/// These are stable system knobs; operational values come from SQL RuntimePolicy table.
/// </summary>
public sealed class RuntimePolicySystemOptions
{
    /// <summary>Provider type: "Sql" (default).</summary>
    public string Provider { get; set; } = "Sql";

    /// <summary>Environment label for policy resolution (e.g. "dev", "staging", "prod").</summary>
    public string Environment { get; set; } = "prod";

    /// <summary>Cache TTL in seconds for resolved policies.</summary>
    public int CacheTtlSeconds { get; set; } = 120;
}
