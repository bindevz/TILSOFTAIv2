using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Http;

public sealed class ConfigurationExternalConnectionCatalog : IExternalConnectionCatalog
{
    private readonly ExternalConnectionCatalogOptions _options;

    public ConfigurationExternalConnectionCatalog(IOptions<ExternalConnectionCatalogOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public ExternalConnectionOptions? Resolve(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return null;
        }

        return _options.Connections.TryGetValue(connectionName, out var connection)
            ? connection
            : null;
    }
}
