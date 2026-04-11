using FluentAssertions;
using TILSOFTAI.Orchestration.Capabilities;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class CapabilityRegistryTests
{
    private static InMemoryCapabilityRegistry CreateRegistry() =>
        new(WarehouseCapabilities.All);

    [Fact]
    public void GetByDomain_ShouldReturnWarehouseCapabilities()
    {
        var registry = CreateRegistry();

        var capabilities = registry.GetByDomain("warehouse");

        capabilities.Should().HaveCount(4);
        capabilities.Select(c => c.CapabilityKey).Should().Contain(new[]
        {
            "warehouse.inventory.summary",
            "warehouse.inventory.by-item",
            "warehouse.receipts.recent",
            "warehouse.external-stock.lookup"
        });
    }

    [Fact]
    public void GetByDomain_ShouldBeCaseInsensitive()
    {
        var registry = CreateRegistry();

        var capabilities = registry.GetByDomain("Warehouse");

        capabilities.Should().HaveCount(4);
    }

    [Fact]
    public void GetByDomain_ShouldReturnEmptyForUnknownDomain()
    {
        var registry = CreateRegistry();

        var capabilities = registry.GetByDomain("sales");

        capabilities.Should().BeEmpty();
    }

    [Fact]
    public void GetByDomain_ShouldReturnEmptyForNullDomain()
    {
        var registry = CreateRegistry();

        var capabilities = registry.GetByDomain(null!);

        capabilities.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShouldReturnCapabilityByKey()
    {
        var registry = CreateRegistry();

        var cap = registry.Resolve("warehouse.inventory.summary");

        cap.Should().NotBeNull();
        cap!.CapabilityKey.Should().Be("warehouse.inventory.summary");
        cap.Domain.Should().Be("warehouse");
        cap.AdapterType.Should().Be("sql");
        cap.Operation.Should().Be("execute_query");
        cap.TargetSystemId.Should().Be("sql");
        cap.ExecutionMode.Should().Be("readonly");
        cap.IntegrationBinding.Should().ContainKey("storedProcedure");
    }

    [Fact]
    public void Resolve_ShouldBeCaseInsensitive()
    {
        var registry = CreateRegistry();

        var cap = registry.Resolve("Warehouse.Inventory.Summary");

        cap.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ShouldReturnNullForUnknownKey()
    {
        var registry = CreateRegistry();

        var cap = registry.Resolve("warehouse.nonexistent");

        cap.Should().BeNull();
    }

    [Fact]
    public void Resolve_ShouldReturnNullForNullKey()
    {
        var registry = CreateRegistry();

        var cap = registry.Resolve(null!);

        cap.Should().BeNull();
    }

    [Fact]
    public void AllWarehouseCapabilities_ShouldBeReadonly()
    {
        var registry = CreateRegistry();
        var capabilities = registry.GetByDomain("warehouse");

        capabilities.Should().OnlyContain(c => c.ExecutionMode == "readonly");
    }

    [Fact]
    public void WarehouseCapabilities_ShouldIncludeSqlAndRestAdapters()
    {
        var registry = CreateRegistry();
        var capabilities = registry.GetByDomain("warehouse");

        capabilities.Where(c => c.AdapterType == "sql").Should().HaveCount(3);
        capabilities.Should().ContainSingle(c =>
            c.AdapterType == "rest-json"
            && c.CapabilityKey == "warehouse.external-stock.lookup");
    }
}
