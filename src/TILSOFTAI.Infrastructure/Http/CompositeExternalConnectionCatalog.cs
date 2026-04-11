using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Http;

public sealed class CompositeExternalConnectionCatalog : IExternalConnectionCatalog
{
    private readonly PlatformExternalConnectionCatalog _platformCatalog;
    private readonly ConfigurationExternalConnectionCatalog _configurationCatalog;
    private readonly PlatformCatalogOptions _options;

    public CompositeExternalConnectionCatalog(
        PlatformExternalConnectionCatalog platformCatalog,
        ConfigurationExternalConnectionCatalog configurationCatalog,
        IOptions<PlatformCatalogOptions> options)
    {
        _platformCatalog = platformCatalog ?? throw new ArgumentNullException(nameof(platformCatalog));
        _configurationCatalog = configurationCatalog ?? throw new ArgumentNullException(nameof(configurationCatalog));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public ExternalConnectionOptions? Resolve(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return null;
        }

        var platformConnection = _platformCatalog.Resolve(connectionName);
        if (platformConnection is not null)
        {
            return platformConnection;
        }

        return _options.AllowBootstrapConfigurationFallback
            ? _configurationCatalog.Resolve(connectionName)
            : null;
    }
}
