using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Modules;

namespace TILSOFTAI.Api.Health;

/// <summary>
/// Health check that verifies enabled modules are loaded.
/// </summary>
public sealed class ModuleHealthCheck : IHealthCheck
{
    private readonly IModuleLoader _moduleLoader;
    private readonly ModulesOptions _options;

    public ModuleHealthCheck(
        IModuleLoader moduleLoader,
        IOptions<ModulesOptions> options)
    {
        _moduleLoader = moduleLoader ?? throw new ArgumentNullException(nameof(moduleLoader));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var loadedModules = _moduleLoader.GetLoadedModules();
        var enabledModules = _options.Enabled ?? Array.Empty<string>();

        var data = new Dictionary<string, object>
        {
            ["enabled_modules"] = enabledModules,
            ["loaded_modules"] = loadedModules.Select(m => m.Name).ToArray()
        };

        var missingModules = enabledModules
            .Where(enabled => !loadedModules.Any(m => 
                m.GetType().Assembly.GetName().Name?.Contains(enabled, StringComparison.OrdinalIgnoreCase) == true))
            .ToList();

        if (missingModules.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Missing modules: {string.Join(", ", missingModules)}",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"All modules loaded: {loadedModules.Count}",
            data: data));
    }
}
