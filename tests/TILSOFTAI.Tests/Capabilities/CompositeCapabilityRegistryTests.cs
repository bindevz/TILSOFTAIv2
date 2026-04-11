using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TILSOFTAI.Orchestration.Capabilities;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class CompositeCapabilityRegistryTests
{
    private static CompositeCapabilityRegistry CreateRegistry(params ICapabilitySource[] sources)
    {
        var logger = new Mock<ILogger<CompositeCapabilityRegistry>>().Object;
        return new CompositeCapabilityRegistry(sources, logger);
    }

    [Fact]
    public void ShouldLoadFromMultipleSources()
    {
        var registry = CreateRegistry(
            new StaticCapabilitySource("warehouse", WarehouseCapabilities.All),
            new StaticCapabilitySource("accounting", AccountingCapabilities.All));

        registry.GetByDomain("warehouse").Should().HaveCount(3);
        registry.GetByDomain("accounting").Should().HaveCount(3);
    }

    [Fact]
    public void ShouldResolveByKey_AcrossSources()
    {
        var registry = CreateRegistry(
            new StaticCapabilitySource("warehouse", WarehouseCapabilities.All),
            new StaticCapabilitySource("accounting", AccountingCapabilities.All));

        var wCap = registry.Resolve("warehouse.inventory.summary");
        var aCap = registry.Resolve("accounting.receivables.summary");

        wCap.Should().NotBeNull();
        wCap!.Domain.Should().Be("warehouse");

        aCap.Should().NotBeNull();
        aCap!.Domain.Should().Be("accounting");
    }

    [Fact]
    public void LaterSourceShouldOverrideEarlierSource()
    {
        var overrideCap = new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.inventory.summary",
            Domain = "warehouse",
            AdapterType = "rest", // different adapter
            Operation = "get",
            TargetSystemId = "rest-api",
            ExecutionMode = "readonly"
        };

        var registry = CreateRegistry(
            new StaticCapabilitySource("warehouse-original", WarehouseCapabilities.All),
            new StaticCapabilitySource("warehouse-override", new[] { overrideCap }));

        var cap = registry.Resolve("warehouse.inventory.summary");

        cap.Should().NotBeNull();
        cap!.AdapterType.Should().Be("rest"); // overridden
    }

    [Fact]
    public void ShouldReturnEmptyForUnknownDomain()
    {
        var registry = CreateRegistry(
            new StaticCapabilitySource("warehouse", WarehouseCapabilities.All));

        registry.GetByDomain("sales").Should().BeEmpty();
    }

    [Fact]
    public void ShouldReturnNullForUnknownKey()
    {
        var registry = CreateRegistry(
            new StaticCapabilitySource("warehouse", WarehouseCapabilities.All));

        registry.Resolve("nonexistent.key").Should().BeNull();
    }

    [Fact]
    public void ShouldHandleEmptySources()
    {
        var registry = CreateRegistry();

        registry.GetByDomain("warehouse").Should().BeEmpty();
        registry.Resolve("warehouse.inventory.summary").Should().BeNull();
    }

    [Fact]
    public void ShouldHandleNullDomain()
    {
        var registry = CreateRegistry(
            new StaticCapabilitySource("warehouse", WarehouseCapabilities.All));

        registry.GetByDomain(null!).Should().BeEmpty();
    }

    [Fact]
    public void ShouldHandleNullKey()
    {
        var registry = CreateRegistry(
            new StaticCapabilitySource("warehouse", WarehouseCapabilities.All));

        registry.Resolve(null!).Should().BeNull();
    }
}
