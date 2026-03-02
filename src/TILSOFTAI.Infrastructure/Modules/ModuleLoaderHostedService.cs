using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Modules;

namespace TILSOFTAI.Infrastructure.Modules;

/// <summary>
/// PATCH 37.01: DB-first module loading with config fallback.
/// Tries IModuleActivationProvider (SQL) first; falls back to ModulesOptions.Enabled.
/// </summary>
public sealed class ModuleLoaderHostedService : IHostedService
{
    private readonly IModuleLoader _moduleLoader;
    private readonly IOptions<ModulesOptions> _modulesOptions;
    private readonly IModuleActivationProvider? _activationProvider;
    private readonly RuntimePolicySystemOptions? _policyOptions;
    private readonly ILogger<ModuleLoaderHostedService> _logger;

    public ModuleLoaderHostedService(
        IModuleLoader moduleLoader,
        IOptions<ModulesOptions> modulesOptions,
        ILogger<ModuleLoaderHostedService> logger,
        IModuleActivationProvider? activationProvider = null,
        IOptions<RuntimePolicySystemOptions>? policyOptions = null)
    {
        _moduleLoader = moduleLoader ?? throw new ArgumentNullException(nameof(moduleLoader));
        _modulesOptions = modulesOptions ?? throw new ArgumentNullException(nameof(modulesOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activationProvider = activationProvider;
        _policyOptions = policyOptions?.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string>? dbModules = null;

        // Try DB-first
        if (_activationProvider is not null)
        {
            try
            {
                var environment = _policyOptions?.Environment;
                dbModules = await _activationProvider.GetEnabledModulesAsync(
                    tenantId: null, // bootstrap: no tenant context yet
                    environment: environment,
                    ct: cancellationToken);

                if (dbModules.Count > 0)
                {
                    _logger.LogInformation(
                        "ModuleActivation | Source: DB | Modules: [{Modules}]",
                        string.Join(", ", dbModules));
                    _moduleLoader.LoadModules(dbModules);
                    return;
                }

                _logger.LogWarning("ModuleActivation | DB returned empty. Falling back to config.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ModuleActivation | DB unavailable. Falling back to config.");
            }
        }

        // Fallback to config
        var configModules = _modulesOptions.Value.Enabled;
        if (configModules.Length > 0)
        {
            _logger.LogInformation(
                "ModuleActivation | Source: Config | Modules: [{Modules}]",
                string.Join(", ", configModules));
            _moduleLoader.LoadModules(configModules);
        }
        else
        {
            // Last resort: load FallbackEnabled (minimal bootstrap)
            var fallback = _modulesOptions.Value.FallbackEnabled;
            if (fallback.Length > 0)
            {
                _logger.LogWarning(
                    "ModuleActivation | Source: FallbackEnabled | Modules: [{Modules}]",
                    string.Join(", ", fallback));
                _moduleLoader.LoadModules(fallback);
            }
            else
            {
                _logger.LogError("ModuleActivation | No modules available from any source.");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
