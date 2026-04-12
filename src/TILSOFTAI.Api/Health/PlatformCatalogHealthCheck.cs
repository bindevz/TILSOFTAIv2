using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Catalog;

namespace TILSOFTAI.Api.Health;

public sealed class PlatformCatalogHealthCheck : IHealthCheck
{
    private readonly IPlatformCatalogProvider _catalogProvider;
    private readonly IConfiguration _configuration;
    private readonly PlatformCatalogOptions _options;

    public PlatformCatalogHealthCheck(
        IPlatformCatalogProvider catalogProvider,
        IConfiguration configuration,
        IOptions<PlatformCatalogOptions> options)
    {
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = _catalogProvider.Load();
        var bootstrapCapabilities = _configuration.GetSection("Capabilities").GetChildren().Count();
        var bootstrapConnections = _configuration.GetSection("ExternalConnections:Connections").GetChildren().Count();
        var mode = DetermineMode(snapshot, bootstrapCapabilities, bootstrapConnections);

        var data = new Dictionary<string, object>
        {
            ["source_mode"] = mode,
            ["catalog_found"] = snapshot.CatalogFound,
            ["catalog_valid"] = snapshot.IsValid,
            ["catalog_path"] = snapshot.CatalogPath,
            ["platform_capability_count"] = snapshot.Capabilities.Count,
            ["platform_connection_count"] = snapshot.ExternalConnections.Count,
            ["bootstrap_capability_count"] = bootstrapCapabilities,
            ["bootstrap_connection_count"] = bootstrapConnections,
            ["bootstrap_fallback_allowed"] = _options.AllowBootstrapConfigurationFallback,
            ["integrity_errors"] = snapshot.IntegrityErrors.ToArray()
        };

        if (!snapshot.IsValid)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Platform catalog integrity validation failed.",
                data: data));
        }

        if (mode == "bootstrap_only")
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Platform catalog is unavailable; runtime is using bootstrap fallback records.",
                data: data));
        }

        if (mode == "mixed")
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Platform catalog is active with bootstrap fallback records also present.",
                data: data));
        }

        if (mode == "empty")
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "No platform catalog or bootstrap catalog records are available.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Platform catalog source-of-truth is active.",
            data));
    }

    private string DetermineMode(
        PlatformCatalogSnapshot snapshot,
        int bootstrapCapabilities,
        int bootstrapConnections)
    {
        var platformCount = snapshot.Capabilities.Count + snapshot.ExternalConnections.Count;
        var bootstrapCount = bootstrapCapabilities + bootstrapConnections;

        if (!_options.Enabled || platformCount == 0)
        {
            return bootstrapCount > 0 && _options.AllowBootstrapConfigurationFallback
                ? "bootstrap_only"
                : "empty";
        }

        return bootstrapCount > 0 && _options.AllowBootstrapConfigurationFallback
            ? "mixed"
            : "platform";
    }
}
