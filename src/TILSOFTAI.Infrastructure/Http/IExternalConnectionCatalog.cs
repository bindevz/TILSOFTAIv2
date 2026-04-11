using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Http;

public interface IExternalConnectionCatalog
{
    ExternalConnectionOptions? Resolve(string connectionName);
}
