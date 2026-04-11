using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogProvider
{
    PlatformCatalogSnapshot Load();
}

public sealed class PlatformCatalogSnapshot
{
    public IReadOnlyList<CapabilityDescriptor> Capabilities { get; init; } = Array.Empty<CapabilityDescriptor>();
    public IReadOnlyDictionary<string, ExternalConnectionOptions> ExternalConnections { get; init; } =
        new Dictionary<string, ExternalConnectionOptions>(StringComparer.OrdinalIgnoreCase);
}
