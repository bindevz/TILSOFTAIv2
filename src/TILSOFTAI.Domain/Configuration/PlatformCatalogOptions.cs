namespace TILSOFTAI.Domain.Configuration;

public sealed class PlatformCatalogOptions
{
    public bool Enabled { get; set; } = true;
    public string CatalogPath { get; set; } = "catalog/platform-catalog.json";
    public bool AllowBootstrapConfigurationFallback { get; set; } = true;
    public string EnvironmentName { get; set; } = "development";
    public string[] ProductionLikeEnvironments { get; set; } = { "prod", "production", "staging" };
    public bool TreatMixedAsUnhealthyInProductionLike { get; set; } = true;
    public bool TreatBootstrapOnlyAsUnhealthyInProductionLike { get; set; } = true;
}
