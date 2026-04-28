using FluentAssertions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;
using Xunit;

namespace TILSOFTAI.Tests.Execution;

public sealed class CompositeCapabilityExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSubCapabilitiesAreReadOnly_ShouldReturnJsonBundle()
    {
        var repository = new StubCapabilityMetadataRepository(
            Composite("sales.customer.360", ["sales.orders.by-customer", "accounting.receivables.by-customer"]),
            ReadOnly("sales.orders.by-customer"),
            ReadOnly("accounting.receivables.by-customer"));
        var facade = new StubCapabilityExecutionFacade();
        var executor = CreateExecutor(repository, facade);

        var result = await executor.ExecuteAsync(
            "sales.customer.360",
            new Dictionary<string, object?> { ["customerCode"] = "CUS-001" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ExecutionMode.Should().Be("composite");
        result.RowCount.Should().Be(2);
        result.ExecutionMetadata.Operation.Should().Be("composite");
        result.Result.Should().BeOfType<CompositeResultBundle>();
        var bundle = (CompositeResultBundle)result.Result!;
        bundle.Output.Should().Be("json_bundle");
        bundle.Sections.Select(section => section.CapabilityKey).Should().Equal(
            "sales.orders.by-customer",
            "accounting.receivables.by-customer");
        facade.ReadCalls.Should().BeEquivalentTo(
            ["sales.orders.by-customer", "accounting.receivables.by-customer"],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnySubCapabilityIsNotReadOnly_ShouldBlockWithoutExecuting()
    {
        var repository = new StubCapabilityMetadataRepository(
            Composite("sales.customer.360", ["sales.orders.by-customer", "sales.order.cancel"]),
            ReadOnly("sales.orders.by-customer"),
            ReadOnly("sales.order.cancel") with { ExecutionMode = "write_preview" });
        var facade = new StubCapabilityExecutionFacade();
        var executor = CreateExecutor(repository, facade);

        var result = await executor.ExecuteAsync(
            "sales.customer.360",
            new Dictionary<string, object?> { ["customerCode"] = "CUS-001" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("blocked");
        result.ErrorMessage.Should().Contain("read-only sub-capabilities");
        facade.ReadCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSubCapabilityCountExceedsLimit_ShouldBlockWithoutExecuting()
    {
        var repository = new StubCapabilityMetadataRepository(
            Composite("sales.customer.360", ["a", "b", "c", "d"]),
            ReadOnly("a"),
            ReadOnly("b"),
            ReadOnly("c"),
            ReadOnly("d"));
        var facade = new StubCapabilityExecutionFacade();
        var executor = CreateExecutor(repository, facade, maxToolCalls: 3);

        var result = await executor.ExecuteAsync(
            "sales.customer.360",
            new Dictionary<string, object?> { ["customerCode"] = "CUS-001" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("blocked");
        result.ErrorMessage.Should().Contain("max sub-capability count");
        facade.ReadCalls.Should().BeEmpty();
    }

    private static CompositeCapabilityExecutor CreateExecutor(
        ICapabilityMetadataRepository repository,
        ICapabilityExecutionFacade facade,
        int maxToolCalls = 3) =>
        new(
            [repository],
            facade,
            [new StubExecutionContextAccessor()],
            Options.Create(new AiRoutingOptions { MaxToolCallsPerTurn = maxToolCalls }));

    private static CapabilitySemanticMetadata Composite(
        string key,
        IReadOnlyList<string> subCapabilities) =>
        Metadata(key) with
        {
            ExecutionMode = "composite",
            AdapterType = "composite",
            Operation = "composite",
            SubCapabilities = System.Text.Json.JsonSerializer.Serialize(subCapabilities),
            CompositionPolicy = """{"mode":"parallel","output":"json_bundle"}"""
        };

    private static CapabilitySemanticMetadata ReadOnly(string key) =>
        Metadata(key) with
        {
            ExecutionMode = "read",
            AdapterType = "sql",
            Operation = "execute_query"
        };

    private static CapabilitySemanticMetadata Metadata(string key) => new()
    {
        CapabilityKey = key,
        Domain = "sales",
        FunctionName = key.Replace('.', '_'),
        AdapterType = "sql",
        Operation = "execute_query",
        ExecutionMode = "read"
    };

    private sealed class StubCapabilityMetadataRepository : ICapabilityMetadataRepository
    {
        private readonly IReadOnlyDictionary<string, CapabilitySemanticMetadata> _items;

        public StubCapabilityMetadataRepository(params CapabilitySemanticMetadata[] items)
        {
            _items = items.ToDictionary(item => item.CapabilityKey, StringComparer.OrdinalIgnoreCase);
        }

        public Task<CapabilitySemanticMetadata?> GetCapabilityMetadataAsync(
            string capabilityKey,
            string locale,
            CancellationToken cancellationToken) =>
            Task.FromResult(_items.TryGetValue(capabilityKey, out var item) ? item : null);
    }

    private sealed class StubCapabilityExecutionFacade : ICapabilityExecutionFacade
    {
        public List<string> ReadCalls { get; } = [];

        public Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
            string capabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            ReadCalls.Add(capabilityKey);
            IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
            [
                new Dictionary<string, object?>
                {
                    ["CapabilityKey"] = capabilityKey,
                    ["CustomerCode"] = arguments.TryGetValue("customerCode", out var code) ? code : null
                }
            ];

            return Task.FromResult(new CapabilityExecutionEnvelope
            {
                CapabilityKey = capabilityKey,
                ExecutionMode = "read",
                Arguments = arguments,
                Rows = rows,
                RowCount = rows.Count,
                Success = true,
                Status = "succeeded",
                ExecutionMetadata = new ExecutionMetadata
                {
                    AdapterType = "sql",
                    Operation = "execute_query"
                },
                SensitivityPolicy = SensitivityPolicy.Default,
                AnswerPolicy = AnswerPolicy.Default,
                Result = rows
            });
        }

        public Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
            string capabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
            string capabilityKey,
            string approvedActionId,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubExecutionContextAccessor : IExecutionContextAccessor
    {
        public TilsoftExecutionContext Current { get; } = new()
        {
            TenantId = "tenant-1",
            UserId = "user-1",
            CorrelationId = "corr-1",
            Language = "en-US"
        };
    }
}
