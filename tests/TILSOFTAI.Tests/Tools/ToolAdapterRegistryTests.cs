using FluentAssertions;
using TILSOFTAI.Tools.Abstractions;
using Xunit;

namespace TILSOFTAI.Tests.Tools;

public sealed class ToolAdapterRegistryTests
{
    [Fact]
    public void ResolveForCapability_ShouldFallBackToSqlAdapter()
    {
        var registry = new ToolAdapterRegistry(new[] { new FakeToolAdapter("sql") });

        var adapter = registry.ResolveForCapability("inventory.adjust", string.Empty);

        adapter.AdapterType.Should().Be("sql");
    }

    private sealed class FakeToolAdapter : IToolAdapter
    {
        public FakeToolAdapter(string adapterType)
        {
            AdapterType = adapterType;
        }

        public string AdapterType { get; }

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken ct)
        {
            return Task.FromResult(ToolExecutionResult.Ok("{}"));
        }
    }
}
