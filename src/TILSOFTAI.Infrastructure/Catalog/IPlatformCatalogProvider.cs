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
    public string Version { get; init; } = string.Empty;
    public string CatalogPath { get; init; } = string.Empty;
    public bool CatalogFound { get; init; }
    public bool IsValid { get; init; } = true;
    public IReadOnlyList<string> IntegrityErrors { get; init; } = Array.Empty<string>();
}
