using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Actions;
using TILSOFTAI.Orchestration.Compaction;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Tools.Abstractions;
using Xunit;

namespace TILSOFTAI.Tests.Approvals;

/// <summary>
/// Sprint 4: End-to-end approval lifecycle tests covering create → approve → execute,
/// and failure paths: pending-should-not-execute, rejected-should-not-execute, re-execute-should-fail.
/// </summary>
public sealed class ApprovalEngineE2ETests
{
    private const string TenantId = "tenant-e2e";
    private const string UserId = "user-e2e";
    private const string ApproverUserId = "approver-e2e";
    private const string SpName = "dbo.sp_warehouse_create_receipt";
    private const string ToolName = "warehouse.receipts.create";

    private readonly InMemoryActionRequestStore _store;
    private readonly ApprovalEngine _engine;
    private readonly Mock<IToolAdapter> _mockAdapter;

    public ApprovalEngineE2ETests()
    {
        _store = new InMemoryActionRequestStore();

        _mockAdapter = new Mock<IToolAdapter>();
        _mockAdapter.SetupGet(a => a.AdapterType).Returns("sql");

        // Catalog lookup: return enabled entry with no required roles and no schema
        _mockAdapter.Setup(a => a.ExecuteAsync(
                It.Is<ToolExecutionRequest>(r => r.Operation == ToolAdapterOperationNames.ExecuteQuery
                    && r.CapabilityKey == "writeaction.catalog.get"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Ok(
                "[{\"ActionName\":\"CreateReceipt\",\"RequiredRoles\":null,\"JsonSchema\":null}]",
                new List<IReadOnlyDictionary<string, object?>>
                {
                    new Dictionary<string, object?>
                    {
                        ["ActionName"] = "CreateReceipt",
                        ["RequiredRoles"] = null,
                        ["JsonSchema"] = null
                    }
                }));

        // Write execution: return success
        _mockAdapter.Setup(a => a.ExecuteAsync(
                It.Is<ToolExecutionRequest>(r => r.Operation == ToolAdapterOperationNames.ExecuteWriteAction),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Ok("{\"receiptId\": \"R-001\"}"));

        var mockAdapterRegistry = new Mock<IToolAdapterRegistry>();
        mockAdapterRegistry.Setup(r => r.Resolve("sql")).Returns(_mockAdapter.Object);

        var chatOptions = Options.Create(new ChatOptions
        {
            CompactionLimits = new Dictionary<string, int> { ["ToolResultMaxBytes"] = 16000 },
            CompactionRules = new CompactionRules()
        });

        var compactor = new ToolResultCompactor();
        var conversationStore = new Mock<IConversationStore>();
        conversationStore.Setup(c => c.SaveToolExecutionAsync(
                It.IsAny<Domain.ExecutionContext.TilsoftExecutionContext>(),
                It.IsAny<ToolExecutionRecord>(),
                It.IsAny<RequestPolicy>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var schemaValidator = new Mock<IJsonSchemaValidator>();
        schemaValidator.Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new JsonSchemaValidationResult(true, Array.Empty<string>(), null));

        _engine = new ApprovalEngine(
            _store,
            mockAdapterRegistry.Object,
            compactor,
            conversationStore.Object,
            chatOptions,
            schemaValidator.Object,
            new Mock<ILogger<ApprovalEngine>>().Object);
    }

    private ApprovalContext CreateContext(string? userId = null) => new()
    {
        TenantId = TenantId,
        UserId = userId ?? UserId,
        Roles = new[] { "warehouse_admin" },
        ConversationId = "conv-e2e",
        CorrelationId = "corr-e2e",
        AgentId = "warehouse"
    };

    private ProposedAction CreateAction() => new()
    {
        ActionType = "write",
        AgentId = "warehouse",
        TargetSystem = "sql",
        CapabilityKey = ToolName,
        PayloadJson = "{\"itemCode\": \"A001\", \"quantity\": 10}",
        ToolName = ToolName,
        StoredProcedure = SpName
    };

    [Fact]
    public async Task Create_ShouldReturnPendingRecord()
    {
        var record = await _engine.CreateAsync(CreateAction(), CreateContext(), CancellationToken.None);

        record.Should().NotBeNull();
        record.Status.Should().Be("Pending");
        record.TenantId.Should().Be(TenantId);
        record.StoredProcedure.Should().Be(SpName);
        record.ActionId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Approve_ShouldReturnApprovedRecord()
    {
        var created = await _engine.CreateAsync(CreateAction(), CreateContext(), CancellationToken.None);

        var approved = await _engine.ApproveAsync(
            created.ActionId, CreateContext(ApproverUserId), CancellationToken.None);

        approved.Should().NotBeNull();
        approved.Status.Should().Be("Approved");
        approved.ActionId.Should().Be(created.ActionId);
    }

    [Fact]
    public async Task Execute_ShouldSucceed_WhenApproved()
    {
        var created = await _engine.CreateAsync(CreateAction(), CreateContext(), CancellationToken.None);
        await _engine.ApproveAsync(created.ActionId, CreateContext(ApproverUserId), CancellationToken.None);

        var result = await _engine.ExecuteAsync(
            created.ActionId, CreateContext(ApproverUserId), CancellationToken.None);

        result.Should().NotBeNull();
        result.Action.Status.Should().Be("Executed");
        result.RawResult.Should().Contain("R-001");
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenPending()
    {
        var created = await _engine.CreateAsync(CreateAction(), CreateContext(), CancellationToken.None);

        var act = () => _engine.ExecuteAsync(
            created.ActionId, CreateContext(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be approved*");
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenRejected()
    {
        var created = await _engine.CreateAsync(CreateAction(), CreateContext(), CancellationToken.None);
        await _engine.RejectAsync(created.ActionId, CreateContext(ApproverUserId), CancellationToken.None);

        var act = () => _engine.ExecuteAsync(
            created.ActionId, CreateContext(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be approved*");
    }

    [Fact]
    public async Task ReExecute_ShouldFail_WhenAlreadyExecuted()
    {
        var created = await _engine.CreateAsync(CreateAction(), CreateContext(), CancellationToken.None);
        await _engine.ApproveAsync(created.ActionId, CreateContext(ApproverUserId), CancellationToken.None);
        await _engine.ExecuteAsync(created.ActionId, CreateContext(ApproverUserId), CancellationToken.None);

        // Attempting to execute again — status is now "Executed", not "Approved"
        var act = () => _engine.ExecuteAsync(
            created.ActionId, CreateContext(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be approved*");
    }

    [Fact]
    public async Task Reject_ShouldReturnRejectedRecord()
    {
        var created = await _engine.CreateAsync(CreateAction(), CreateContext(), CancellationToken.None);

        var rejected = await _engine.RejectAsync(
            created.ActionId, CreateContext(ApproverUserId), CancellationToken.None);

        rejected.Should().NotBeNull();
        rejected.Status.Should().Be("Rejected");
    }

    [Fact]
    public async Task FullLifecycle_Create_Approve_Execute()
    {
        // Step 1: Create
        var action = CreateAction();
        var context = CreateContext();
        var created = await _engine.CreateAsync(action, context, CancellationToken.None);
        created.Status.Should().Be("Pending");

        // Step 2: Approve
        var approveContext = CreateContext(ApproverUserId);
        var approved = await _engine.ApproveAsync(created.ActionId, approveContext, CancellationToken.None);
        approved.Status.Should().Be("Approved");

        // Step 3: Execute
        var result = await _engine.ExecuteAsync(created.ActionId, approveContext, CancellationToken.None);
        result.Action.Status.Should().Be("Executed");
        result.RawResult.Should().NotBeNullOrEmpty();

        // Step 4: Re-execute should fail
        var reExec = () => _engine.ExecuteAsync(created.ActionId, approveContext, CancellationToken.None);
        await reExec.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// In-memory action request store for E2E testing.
    /// Simulates the SQL-backed store with identical contract behavior.
    /// </summary>
    private sealed class InMemoryActionRequestStore : IActionRequestStore
    {
        private readonly Dictionary<string, ActionRequestRecord> _records = new(StringComparer.OrdinalIgnoreCase);
        private int _sequence;

        public Task<ActionRequestRecord> CreateAsync(ActionRequestRecord request, CancellationToken cancellationToken)
        {
            var id = $"action-{Interlocked.Increment(ref _sequence)}";
            request.ActionId = id;
            request.RequestedAtUtc = DateTime.UtcNow;
            _records[id] = request;
            return Task.FromResult(request);
        }

        public Task<ActionRequestRecord?> GetAsync(string tenantId, string actionId, CancellationToken cancellationToken)
        {
            _records.TryGetValue(actionId, out var record);
            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord> ApproveAsync(string tenantId, string actionId, string approvedByUserId, CancellationToken cancellationToken)
        {
            var record = _records[actionId];
            record.Status = "Approved";
            record.ApprovedByUserId = approvedByUserId;
            record.ApprovedAtUtc = DateTime.UtcNow;
            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord> RejectAsync(string tenantId, string actionId, string approvedByUserId, CancellationToken cancellationToken)
        {
            var record = _records[actionId];
            record.Status = "Rejected";
            record.ApprovedByUserId = approvedByUserId;
            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord> MarkExecutedAsync(string tenantId, string actionId, string resultCompactJson, bool success, CancellationToken cancellationToken)
        {
            var record = _records[actionId];
            record.Status = "Executed";
            record.ExecutedAtUtc = DateTime.UtcNow;
            record.ExecutionResultCompactJson = resultCompactJson;
            return Task.FromResult(record);
        }
    }
}
