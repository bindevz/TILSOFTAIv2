namespace TILSOFTAI.Domain.Configuration;

public sealed class PlatformCatalogOptions
{
    public bool Enabled { get; set; } = true;
    public string CatalogPath { get; set; } = "catalog/platform-catalog.json";
    public bool AllowBootstrapConfigurationFallback { get; set; } = true;
}
