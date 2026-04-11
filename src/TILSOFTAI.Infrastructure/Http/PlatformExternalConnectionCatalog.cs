using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Catalog;

namespace TILSOFTAI.Infrastructure.Http;

public sealed class PlatformExternalConnectionCatalog : IExternalConnectionCatalog
{
    private readonly IPlatformCatalogProvider _catalogProvider;

    public PlatformExternalConnectionCatalog(IPlatformCatalogProvider catalogProvider)
    {
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
    }

    public ExternalConnectionOptions? Resolve(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return null;
        }

        return _catalogProvider.Load().ExternalConnections.TryGetValue(connectionName, out var connection)
            ? connection
            : null;
    }
}
