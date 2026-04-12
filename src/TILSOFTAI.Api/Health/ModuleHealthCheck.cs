using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Modules;

namespace TILSOFTAI.Api.Health;

/// <summary>
/// Health check that verifies enabled modules are loaded.
/// </summary>
#pragma warning disable CS0618 // Legacy diagnostic endpoint only; not part of /health/ready.
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
            ["legacy_autoload_enabled"] = _options.EnableLegacyAutoload,
            ["enabled_modules"] = enabledModules,
            ["loaded_modules"] = loadedModules.Select(m => m.Name).ToArray(),
            ["module_classifications"] = _options.Classifications
        };

        if (!_options.EnableLegacyAutoload)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Legacy module autoload is disabled; native runtime readiness is checked separately.",
                data: data));
        }

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
#pragma warning restore CS0618
