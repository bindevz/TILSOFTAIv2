using Microsoft.Extensions.Logging;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class PlatformCatalogCapabilitySource : ICapabilitySource
{
    private readonly IPlatformCatalogProvider _catalogProvider;
    private readonly ILogger<PlatformCatalogCapabilitySource> _logger;

    public PlatformCatalogCapabilitySource(
        IPlatformCatalogProvider catalogProvider,
        ILogger<PlatformCatalogCapabilitySource> logger)
    {
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string SourceName => "platform-catalog";

    public IReadOnlyList<CapabilityDescriptor> Load()
    {
        var capabilities = _catalogProvider.Load().Capabilities;
        _logger.LogInformation(
            "PlatformCatalogCapabilitySource | Loaded {Count} durable capability records",
            capabilities.Count);
        return capabilities;
    }
}
