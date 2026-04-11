using Microsoft.Extensions.Diagnostics.HealthChecks;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Api.Health;

/// <summary>
/// Readiness check for the supervisor-native runtime path.
/// </summary>
public sealed class NativeRuntimeHealthCheck : IHealthCheck
{
    private readonly ISupervisorRuntime _supervisorRuntime;
    private readonly ICapabilityRegistry _capabilityRegistry;
    private readonly IToolAdapterRegistry _toolAdapterRegistry;

    public NativeRuntimeHealthCheck(
        ISupervisorRuntime supervisorRuntime,
        ICapabilityRegistry capabilityRegistry,
        IToolAdapterRegistry toolAdapterRegistry)
    {
        _supervisorRuntime = supervisorRuntime ?? throw new ArgumentNullException(nameof(supervisorRuntime));
        _capabilityRegistry = capabilityRegistry ?? throw new ArgumentNullException(nameof(capabilityRegistry));
        _toolAdapterRegistry = toolAdapterRegistry ?? throw new ArgumentNullException(nameof(toolAdapterRegistry));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        _ = _supervisorRuntime;

        var warehouseCapabilities = _capabilityRegistry.GetByDomain("warehouse");
        var accountingCapabilities = _capabilityRegistry.GetByDomain("accounting");
        var capabilities = warehouseCapabilities.Concat(accountingCapabilities).ToArray();

        var data = new Dictionary<string, object>
        {
            ["warehouse_capabilities"] = warehouseCapabilities.Count,
            ["accounting_capabilities"] = accountingCapabilities.Count,
            ["adapter_types"] = capabilities
                .Select(capability => capability.AdapterType)
                .Where(adapterType => !string.IsNullOrWhiteSpace(adapterType))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        if (warehouseCapabilities.Count == 0 || accountingCapabilities.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Native runtime capabilities are not fully loaded.",
                data: data));
        }

        foreach (var adapterType in capabilities
            .Select(capability => capability.AdapterType)
            .Where(adapterType => !string.IsNullOrWhiteSpace(adapterType))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                _toolAdapterRegistry.Resolve(adapterType);
            }
            catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Native runtime adapter is not registered: {adapterType}",
                    ex,
                    data));
            }
        }

        return Task.FromResult(HealthCheckResult.Healthy("Native supervisor runtime is ready.", data));
    }
}
