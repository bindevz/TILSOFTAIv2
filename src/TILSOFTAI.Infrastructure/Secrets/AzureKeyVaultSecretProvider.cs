using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Secrets;

namespace TILSOFTAI.Infrastructure.Secrets;

/// <summary>
/// Secret provider that reads from Azure Key Vault.
/// </summary>
public sealed class AzureKeyVaultSecretProvider : ISecretProvider
{
    private readonly SecretClient _client;
    private readonly IMemoryCache _cache;
    private readonly SecretsOptions _options;
    private readonly ILogger<AzureKeyVaultSecretProvider> _logger;

    public string ProviderName => "AzureKeyVault";

    public AzureKeyVaultSecretProvider(
        IOptions<SecretsOptions> options,
        IMemoryCache cache,
        ILogger<AzureKeyVaultSecretProvider> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(_options.AzureKeyVaultUri))
        {
            throw new InvalidOperationException("AzureKeyVaultUri is required for AzureKeyVault provider");
        }

        _client = new SecretClient(
            new Uri(_options.AzureKeyVaultUri),
            new DefaultAzureCredential());
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"akv:{key}";
        
        if (_cache.TryGetValue(cacheKey, out string? cachedValue))
        {
            return cachedValue;
        }

        try
        {
            // Convert path to Key Vault secret name (replace / with -)
            var secretName = key.Replace("/", "-");
            var response = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            var value = response?.Value?.Value;

            if (value is not null)
            {
                _cache.Set(cacheKey, value, TimeSpan.FromSeconds(_options.CacheSeconds));
            }

            return value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Secret not found in Key Vault: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret from Key Vault: {Key}", key);
            throw;
        }
    }

    public async Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetSecretAsync(key, cancellationToken);
        return value is not null;
    }
}
