using FluentAssertions;
using TILSOFTAI.Orchestration.Capabilities;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class CapabilityRegistryTests
{
    private static InMemoryCapabilityRegistry CreateRegistry() =>
        new(ModelCapabilities.All);

    [Fact]
    public void GetByDomain_ShouldReturnModelCapabilities()
    {
        var registry = CreateRegistry();

        var capabilities = registry.GetByDomain("model");

        capabilities.Should().HaveCount(6);
        capabilities.Select(c => c.CapabilityKey).Should().BeEquivalentTo(
            "model.count",
            "model.overview.by-code",
            "model.pieces.by-code",
            "model.materials.by-code",
            "model.compare",
            "model.packaging.by-code");
    }

    [Fact]
    public void GetByDomain_ShouldBeCaseInsensitive()
    {
        var registry = CreateRegistry();

        var capabilities = registry.GetByDomain("Model");

        capabilities.Should().HaveCount(6);
    }

    [Fact]
    public void GetByDomain_ShouldReturnEmptyForUnknownDomain()
    {
        var registry = CreateRegistry();

        registry.GetByDomain("sales").Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ShouldReturnCapabilityByKey()
    {
        var registry = CreateRegistry();

        var cap = registry.Resolve("model.overview.by-code");

        cap.Should().NotBeNull();
        cap!.CapabilityKey.Should().Be("model.overview.by-code");
        cap.Domain.Should().Be("model");
        cap.AdapterType.Should().Be("sql");
        cap.Operation.Should().Be("execute_tool");
        cap.TargetSystemId.Should().Be("sql");
        cap.ExecutionMode.Should().Be("readonly");
        cap.IntegrationBinding.Should().ContainKey("storedProcedure");
    }

    [Fact]
    public void Resolve_ShouldBeCaseInsensitive()
    {
        var registry = CreateRegistry();

        registry.Resolve("Model.Overview.By-Code").Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ShouldReturnNullForUnknownKey()
    {
        var registry = CreateRegistry();

        registry.Resolve("model.nonexistent").Should().BeNull();
    }

    [Fact]
    public void AllModelCapabilities_ShouldBeReadonly()
    {
        var registry = CreateRegistry();
        var capabilities = registry.GetByDomain("model");

        capabilities.Should().OnlyContain(c => c.ExecutionMode == "readonly");
    }
}
