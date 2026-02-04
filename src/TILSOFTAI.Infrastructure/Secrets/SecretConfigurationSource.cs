using Microsoft.Extensions.Configuration;
using TILSOFTAI.Domain.Secrets;

namespace TILSOFTAI.Infrastructure.Secrets;

/// <summary>
/// Configuration source that loads secrets into configuration.
/// </summary>
public sealed class SecretConfigurationSource : IConfigurationSource
{
    private readonly ISecretProvider _provider;

    public SecretConfigurationSource(ISecretProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new SecretConfigurationProvider(_provider);
    }
}

internal sealed class SecretConfigurationProvider : ConfigurationProvider
{
    private readonly ISecretProvider _provider;

    public SecretConfigurationProvider(ISecretProvider provider)
    {
        _provider = provider;
    }

    public override void Load()
    {
        foreach (var (configKey, secretKey) in SecretKeys.ConfigToSecretMap)
        {
            var value = _provider.GetSecretAsync(secretKey).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(value))
            {
                Set(configKey, value);
            }
        }
    }
}
