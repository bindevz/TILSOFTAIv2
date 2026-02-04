namespace TILSOFTAI.Domain.Configuration;

public sealed class SecretsOptions
{
    /// <summary>
    /// Secret provider type.
    /// </summary>
    public string Provider { get; set; } = "Environment";

    /// <summary>
    /// Azure Key Vault URI (when Provider = AzureKeyVault).
    /// </summary>
    public string? AzureKeyVaultUri { get; set; }

    /// <summary>
    /// AWS region (when Provider = AwsSecretsManager).
    /// </summary>
    public string? AwsRegion { get; set; }

    /// <summary>
    /// HashiCorp Vault address (when Provider = HashiCorpVault).
    /// </summary>
    public string? VaultAddress { get; set; }

    /// <summary>
    /// How long to cache secrets in memory (seconds).
    /// </summary>
    public int CacheSeconds { get; set; } = 300;
}
