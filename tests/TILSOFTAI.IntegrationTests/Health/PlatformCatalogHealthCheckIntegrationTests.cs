using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Health;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Orchestration.Capabilities;
using Xunit;

namespace TILSOFTAI.IntegrationTests.Health;

public sealed class PlatformCatalogHealthCheckIntegrationTests
{
    [Fact]
    public async Task CheckHealthAsync_ShouldBeUnhealthyForMixedSourceMode_InProductionLikeEnvironment()
    {
        var check = new PlatformCatalogHealthCheck(
            new StubCatalogProvider(PlatformSnapshot()),
            BootstrapConfiguration(),
            Options.Create(new PlatformCatalogOptions
            {
                EnvironmentName = "prod",
                AllowBootstrapConfigurationFallback = true,
                TreatMixedAsUnhealthyInProductionLike = true
            }));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data["source_mode"].Should().Be("mixed");
        result.Data["production_like"].Should().Be(true);
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldBeDegradedForMixedSourceMode_InDevelopmentEnvironment()
    {
        var check = new PlatformCatalogHealthCheck(
            new StubCatalogProvider(PlatformSnapshot()),
            BootstrapConfiguration(),
            Options.Create(new PlatformCatalogOptions
            {
                EnvironmentName = "development",
                AllowBootstrapConfigurationFallback = true
            }));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data["source_mode"].Should().Be("mixed");
        result.Data["production_like"].Should().Be(false);
    }

    private static IConfiguration BootstrapConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Capabilities:0:CapabilityKey"] = "warehouse.bootstrap",
            ["ExternalConnections:Connections:bootstrap-only:BaseUrl"] = "https://bootstrap.test"
        })
        .Build();

    private static PlatformCatalogSnapshot PlatformSnapshot() => new()
    {
        CatalogFound = true,
        IsValid = true,
        Capabilities = new[]
        {
            new CapabilityDescriptor
            {
                CapabilityKey = "warehouse.platform",
                Domain = "warehouse",
                AdapterType = "sql",
                Operation = "execute_query",
                TargetSystemId = "sql",
                ArgumentContract = new CapabilityArgumentContract { AllowAdditionalArguments = false }
            }
        },
        ExternalConnections = new Dictionary<string, ExternalConnectionOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["platform"] = new() { BaseUrl = "https://platform.test" }
        }
    };

    private sealed class StubCatalogProvider : IPlatformCatalogProvider
    {
        private readonly PlatformCatalogSnapshot _snapshot;

        public StubCatalogProvider(PlatformCatalogSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public PlatformCatalogSnapshot Load() => _snapshot;
    }
}
