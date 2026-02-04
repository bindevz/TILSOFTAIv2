namespace TILSOFTAI.Domain.Secrets;

/// <summary>
/// Abstraction for retrieving secrets from various backends.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Provider identifier (e.g., "Environment", "AzureKeyVault").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Retrieves a secret value by key.
    /// </summary>
    /// <param name="key">The secret key/path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value, or null if not found.</returns>
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a secret exists.
    /// </summary>
    Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default);
}
