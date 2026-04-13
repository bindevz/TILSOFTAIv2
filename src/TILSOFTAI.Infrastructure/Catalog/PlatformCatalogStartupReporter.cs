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
    private readonly IPlatformCatalogSignerTrustStore _signerTrustStore;
    private readonly IPlatformCatalogArchiveStorage _archiveStorage;
    private readonly IConfiguration _configuration;
    private readonly PlatformCatalogOptions _options;
    private readonly IMetricsService _metrics;
    private readonly ILogger<PlatformCatalogStartupReporter> _logger;

    public PlatformCatalogStartupReporter(
        IPlatformCatalogProvider catalogProvider,
        IPlatformCatalogSignerTrustStore signerTrustStore,
        IPlatformCatalogArchiveStorage archiveStorage,
        IConfiguration configuration,
        IOptions<PlatformCatalogOptions> options,
        IMetricsService metrics,
        ILogger<PlatformCatalogStartupReporter> logger)
    {
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        _signerTrustStore = signerTrustStore ?? throw new ArgumentNullException(nameof(signerTrustStore));
        _archiveStorage = archiveStorage ?? throw new ArgumentNullException(nameof(archiveStorage));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var snapshot = _catalogProvider.Load();
        var signers = _signerTrustStore.ListSigners();
        var activeSigners = signers.Count(item => string.Equals(item.Status, CatalogSignerLifecycleStates.Active, StringComparison.OrdinalIgnoreCase));
        var bootstrapCapabilities = _configuration.GetSection("Capabilities").GetChildren().Count();
        var bootstrapConnections = _configuration.GetSection("ExternalConnections:Connections").GetChildren().Count();
        var mode = DetermineMode(snapshot, bootstrapCapabilities, bootstrapConnections);
        var productionLike = IsProductionLike();

        _metrics.IncrementCounter(MetricNames.PlatformCatalogSourceModeTotal, new Dictionary<string, string>
        {
            ["mode"] = mode,
            ["environment"] = EffectiveEnvironmentName(),
            ["production_like"] = productionLike ? "true" : "false",
            ["platform_valid"] = snapshot.IsValid ? "true" : "false"
        });

        _logger.LogInformation(
            "PlatformCatalogSourceReport | Mode: {Mode} | Environment: {Environment} | ProductionLike: {ProductionLike} | CatalogFound: {CatalogFound} | CatalogValid: {CatalogValid} | PlatformCapabilities: {PlatformCapabilities} | PlatformConnections: {PlatformConnections} | BootstrapCapabilities: {BootstrapCapabilities} | BootstrapConnections: {BootstrapConnections} | BootstrapFallbackAllowed: {BootstrapFallbackAllowed}",
            mode,
            EffectiveEnvironmentName(),
            productionLike,
            snapshot.CatalogFound,
            snapshot.IsValid,
            snapshot.Capabilities.Count,
            snapshot.ExternalConnections.Count,
            bootstrapCapabilities,
            bootstrapConnections,
            _options.AllowBootstrapConfigurationFallback);

        _logger.LogInformation(
            "PlatformCatalogTrustInfrastructureReport | SignerCount: {SignerCount} | ActiveSigners: {ActiveSigners} | ArchiveBackend: {ArchiveBackend}",
            signers.Count,
            activeSigners,
            _archiveStorage.BackendName);

        if (productionLike && activeSigners == 0)
        {
            _logger.LogWarning(
                "PlatformCatalogSignerTrustMissing | Environment: {Environment} | Production-like signature policy requires active trusted signers before signed evidence can verify.",
                EffectiveEnvironmentName());
        }

        if (mode is "bootstrap_only" or "mixed")
        {
            if (productionLike)
            {
                _logger.LogError(
                    "PlatformCatalogBootstrapFallbackProductionRisk | Mode: {Mode} | Environment: {Environment} | Bootstrap catalog fallback is not acceptable as normal production source-of-truth.",
                    mode,
                    EffectiveEnvironmentName());
            }
            else
            {
                _logger.LogWarning(
                    "PlatformCatalogBootstrapFallbackActive | Mode: {Mode} | Bootstrap configuration is active and must not be treated as durable production source-of-truth.",
                    mode);
            }
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

    private bool IsProductionLike() =>
        _options.ProductionLikeEnvironments.Any(environment =>
            string.Equals(environment, EffectiveEnvironmentName(), StringComparison.OrdinalIgnoreCase));

    private string EffectiveEnvironmentName() =>
        string.IsNullOrWhiteSpace(_options.EnvironmentName) ? "development" : _options.EnvironmentName.Trim();
}
