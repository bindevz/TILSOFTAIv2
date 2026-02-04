using Microsoft.Extensions.Diagnostics.HealthChecks;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Api.Health;

/// <summary>
/// Health check that verifies tool catalog is loaded.
/// </summary>
public sealed class ToolCatalogHealthCheck : IHealthCheck
{
    private readonly IToolCatalogResolver _catalogResolver;
    private readonly IToolRegistry _registry;

    public ToolCatalogHealthCheck(
        IToolCatalogResolver catalogResolver,
        IToolRegistry registry)
    {
        _catalogResolver = catalogResolver ?? throw new ArgumentNullException(nameof(catalogResolver));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tools = await _catalogResolver.GetResolvedToolsAsync(cancellationToken);
            var registeredCount = _registry.ListEnabled().Count;

            var data = new Dictionary<string, object>
            {
                ["resolved_tools"] = tools.Count,
                ["registered_tools"] = registeredCount
            };

            if (tools.Count == 0)
            {
                return HealthCheckResult.Unhealthy(
                    "No tools resolved from catalog",
                    data: data);
            }

            if (tools.Count != registeredCount)
            {
                return HealthCheckResult.Degraded(
                    $"Tool count mismatch: resolved={tools.Count}, registered={registeredCount}",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Tool catalog healthy: {tools.Count} tools",
                data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Tool catalog check failed: {ex.Message}");
        }
    }
}
