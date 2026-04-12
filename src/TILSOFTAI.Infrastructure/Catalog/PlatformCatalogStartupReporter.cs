using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class PlatformCatalogStartupReporter : IHostedService
{
    private readonly IPlatformCatalogProvider _catalogProvider;
    private readonly IConfiguration _configuration;
    private readonly PlatformCatalogOptions _options;
    private readonly IMetricsService _metrics;
    private readonly ILogger<PlatformCatalogStartupReporter> _logger;

    public PlatformCatalogStartupReporter(
        IPlatformCatalogProvider catalogProvider,
        IConfiguration configuration,
        IOptions<PlatformCatalogOptions> options,
        IMetricsService metrics,
        ILogger<PlatformCatalogStartupReporter> logger)
    {
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var snapshot = _catalogProvider.Load();
        var bootstrapCapabilities = _configuration.GetSection("Capabilities").GetChildren().Count();
        var bootstrapConnections = _configuration.GetSection("ExternalConnections:Connections").GetChildren().Count();
        var mode = DetermineMode(snapshot, bootstrapCapabilities, bootstrapConnections);

        _metrics.IncrementCounter(MetricNames.PlatformCatalogSourceModeTotal, new Dictionary<string, string>
        {
            ["mode"] = mode,
            ["platform_valid"] = snapshot.IsValid ? "true" : "false"
        });

        _logger.LogInformation(
            "PlatformCatalogSourceReport | Mode: {Mode} | CatalogFound: {CatalogFound} | CatalogValid: {CatalogValid} | PlatformCapabilities: {PlatformCapabilities} | PlatformConnections: {PlatformConnections} | BootstrapCapabilities: {BootstrapCapabilities} | BootstrapConnections: {BootstrapConnections} | BootstrapFallbackAllowed: {BootstrapFallbackAllowed}",
            mode,
            snapshot.CatalogFound,
            snapshot.IsValid,
            snapshot.Capabilities.Count,
            snapshot.ExternalConnections.Count,
            bootstrapCapabilities,
            bootstrapConnections,
            _options.AllowBootstrapConfigurationFallback);

        if (mode is "bootstrap_only" or "mixed")
        {
            _logger.LogWarning(
                "PlatformCatalogBootstrapFallbackActive | Mode: {Mode} | Bootstrap configuration is active and must not be treated as durable production source-of-truth.",
                mode);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
