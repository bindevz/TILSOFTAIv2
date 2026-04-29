using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;
using TILSOFTAI.Tools.Abstractions;
using Xunit;

namespace TILSOFTAI.Tests.Execution;

public sealed class CapabilityExecutionFacadeTests
{
    [Fact]
    public async Task ExecuteReadAsync_WhenRequiredArgumentMissing_ReturnsClarificationWithoutCallingAdapter()
    {
        var adapter = new RecordingAdapter(ToolExecutionResult.Ok("[]", Array.Empty<IReadOnlyDictionary<string, object?>>()));
        var facade = CreateFacade(
            Capability(requiredRoles: []),
            adapter,
            Context(roles: ["warehouse.read"]));

        var result = await facade.ExecuteReadAsync(
            "warehouse.inventory.by-item",
            new Dictionary<string, object?>(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(CapabilityExecutionFacade.ArgumentValidationFailedCode);
        result.MissingArguments.Should().Contain("ItemNo");
        result.ClarificationQuestion.Should().NotBeNullOrWhiteSpace();
        adapter.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteReadAsync_WhenRoleMissing_BlocksWithoutCallingAdapter()
    {
        var adapter = new RecordingAdapter(ToolExecutionResult.Ok("[]", Array.Empty<IReadOnlyDictionary<string, object?>>()));
        var facade = CreateFacade(
            Capability(requiredRoles: ["warehouse.read"]),
            adapter,
            Context(roles: ["accounting.read"]));

        var result = await facade.ExecuteReadAsync(
            "warehouse.inventory.by-item",
            new Dictionary<string, object?> { ["item_no"] = "MADEIRA-BLK" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(CapabilityAccessPolicy.AccessDeniedCode);
        adapter.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteReadAsync_WhenValid_MapsArgumentsAndCallsAdapter()
    {
        var rows = new[]
        {
            new Dictionary<string, object?> { ["ItemNo"] = "MADEIRA-BLK", ["Qty"] = 12m }
        };
        var adapter = new RecordingAdapter(ToolExecutionResult.Ok(null, rows));
        var facade = CreateFacade(
            Capability(requiredRoles: ["warehouse.read"]),
            adapter,
            Context(roles: ["warehouse.read"]));

        var result = await facade.ExecuteReadAsync(
            "warehouse.inventory.by-item",
            new Dictionary<string, object?> { ["item_no"] = "MADEIRA-BLK" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RowCount.Should().Be(1);
        adapter.Calls.Should().Be(1);
        adapter.LastRequest.Should().NotBeNull();
        adapter.LastRequest!.Operation.Should().Be(ToolAdapterOperationNames.ExecuteQuery);
        adapter.LastRequest.Metadata["storedProcedure"].Should().Be("dbo.ai_warehouse_inventory_by_item");
        adapter.LastRequest.ArgumentsJson.Should().Contain("@ItemNo");
        adapter.LastRequest.ArgumentsJson.Should().Contain("MADEIRA-BLK");
    }

    [Fact]
    public async Task RuntimeResultParsing_EnvelopeObject_ReturnsRows()
    {
        var result = await ExecuteReadWithPayloadAsync(
            """{"meta":{"source":"sql"},"columns":["ItemNo","Qty"],"rows":[{"ItemNo":"A","Qty":6}]}""");

        result.Success.Should().BeTrue();
        result.RowCount.Should().Be(1);
        result.Rows[0]["ItemNo"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("A");
    }

    [Fact]
    public async Task RuntimeResultParsing_DirectRowArray_ReturnsRows()
    {
        var result = await ExecuteReadWithPayloadAsync("""[{"ItemNo":"A","Qty":6},{"ItemNo":"B","Qty":4}]""");

        result.Success.Should().BeTrue();
        result.RowCount.Should().Be(2);
    }

    [Fact]
    public async Task RuntimeResultParsing_ScalarResult_ReturnsSingleRow()
    {
        var result = await ExecuteReadWithPayloadAsync("""{"count":6}""");

        result.Success.Should().BeTrue();
        result.RowCount.Should().Be(1);
        result.Rows[0]["count"].Should().BeOfType<JsonElement>().Which.GetInt32().Should().Be(6);
    }

    [Fact]
    public async Task RuntimeResultParsing_EmptyResult_ReturnsEmptyRows()
    {
        var result = await ExecuteReadWithPayloadAsync("""{"rows":[]}""");

        result.Success.Should().BeTrue();
        result.RowCount.Should().Be(0);
        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task RuntimeResultParsing_MalformedJson_ReturnsExplicitFailure()
    {
        var result = await ExecuteReadWithPayloadAsync("""{"rows":[""");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SQL_RESULT_PARSE_FAILED");
        result.ErrorMessage.Should().Contain("Malformed SQL JSON");
        result.ErrorMessage.Should().Contain("corr-exec");
    }

    [Fact]
    public async Task RuntimeResultParsing_UnexpectedSqlShape_ReturnsExplicitFailureWithCorrelationId()
    {
        var result = await ExecuteReadWithPayloadAsync("""42""");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SQL_RESULT_PARSE_FAILED");
        result.ErrorMessage.Should().Contain("Unexpected SQL result shape");
        result.ErrorMessage.Should().Contain("corr-exec");
    }

    [Fact]
    public async Task PreviewWriteAsync_WhenValid_CreatesApprovalDraftWithoutCallingAdapter()
    {
        var adapter = new RecordingAdapter(ToolExecutionResult.Fail("SHOULD_NOT_CALL"));
        var approval = new RecordingApprovalEngine();
        var facade = CreateFacade(
            WritePreviewCapability(),
            adapter,
            Context(roles: ["sales.write"]),
            approval);

        var result = await facade.PreviewWriteAsync(
            "sales.order.create-preview",
            new Dictionary<string, object?> { ["customer_code"] = "C001" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("preview");
        result.DraftAction.Should().NotBeNull();
        result.DraftAction!["actionId"].Should().Be("action-1");
        result.ExecutionMetadata.Operation.Should().Be("write_preview");
        approval.CreatedActions.Should().ContainSingle();
        adapter.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteApprovedWriteAsync_WhenNoApprovedActionId_BlocksBeforeAdapter()
    {
        var adapter = new RecordingAdapter(ToolExecutionResult.Fail("SHOULD_NOT_CALL"));
        var facade = CreateFacade(
            WriteExecuteCapability(),
            adapter,
            Context(roles: ["sales.write"]));

        var result = await facade.ExecuteApprovedWriteAsync(
            "sales.order.create-execute",
            string.Empty,
            new Dictionary<string, object?> { ["customer_code"] = "C001" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("WRITE_APPROVAL_REQUIRED");
        adapter.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteApprovedWriteAsync_WhenApprovalEngineAvailable_DelegatesExecutionToApprovalEngine()
    {
        var adapter = new RecordingAdapter(ToolExecutionResult.Fail("SHOULD_NOT_CALL"));
        var approval = new RecordingApprovalEngine();
        var facade = CreateFacade(
            WriteExecuteCapability(),
            adapter,
            Context(roles: ["sales.write"]),
            approval);

        var result = await facade.ExecuteApprovedWriteAsync(
            "sales.order.create-execute",
            "action-1",
            new Dictionary<string, object?> { ["customer_code"] = "C001" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("succeeded");
        approval.ExecutedActionIds.Should().ContainSingle().Which.Should().Be("action-1");
        approval.ExpectedPayloadJson.Should().Contain("@CustomerCode");
        approval.ExpectedPayloadJson.Should().Contain("C001");
        adapter.Calls.Should().Be(0);
    }

    private static CapabilityExecutionFacade CreateFacade(
        CapabilityDescriptor capability,
        IToolAdapter adapter,
        TilsoftExecutionContext context,
        IApprovalEngine? approvalEngine = null) => new(
            new SingleCapabilityRegistry(capability),
            Array.Empty<ICapabilityMetadataRepository>(),
            new[] { new FixedExecutionContextAccessor(context) },
            approvalEngine is null ? Array.Empty<IApprovalEngine>() : new[] { approvalEngine },
            new SingleAdapterRegistry(adapter),
            new CapabilityArgumentMapper(),
            new CapabilityExecutionPolicy(),
            NullLogger<CapabilityExecutionFacade>.Instance);

    private static Task<CapabilityExecutionEnvelope> ExecuteReadWithPayloadAsync(string payloadJson)
    {
        var context = Context(roles: ["warehouse.read"]);
        context.CorrelationId = "corr-exec";
        var facade = CreateFacade(
            Capability(requiredRoles: ["warehouse.read"]),
            new RecordingAdapter(ToolExecutionResult.Ok(payloadJson)),
            context);

        return facade.ExecuteReadAsync(
            "warehouse.inventory.by-item",
            new Dictionary<string, object?> { ["item_no"] = "MADEIRA-BLK" },
            CancellationToken.None);
    }

    private static CapabilityDescriptor Capability(IReadOnlyList<string> requiredRoles) => new()
    {
        CapabilityKey = "warehouse.inventory.by-item",
        Domain = "warehouse",
        AdapterType = "sql",
        Operation = "read",
        TargetSystemId = "sql",
        ExecutionMode = "read_only",
        RequiredRoles = requiredRoles,
        IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storedProcedure"] = "dbo.ai_warehouse_inventory_by_item"
        },
        ArgumentContract = new CapabilityArgumentContract
        {
            RequiredArguments = ["@ItemNo"],
            AllowedArguments = ["@ItemNo"],
            AllowAdditionalArguments = false,
            Arguments =
            [
                new CapabilityArgumentRule
                {
                    Name = "@ItemNo",
                    Type = "string",
                    Format = "item-number"
                }
            ]
        }
    };

    private static CapabilityDescriptor WritePreviewCapability() => new()
    {
        CapabilityKey = "sales.order.create-preview",
        Domain = "sales",
        AdapterType = "sql",
        Operation = "write_preview",
        TargetSystemId = "sql",
        ExecutionMode = "write_preview",
        RequiredRoles = ["sales.write"],
        IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storedProcedure"] = "dbo.sales_order_create"
        },
        ArgumentContract = new CapabilityArgumentContract
        {
            RequiredArguments = ["@CustomerCode"],
            AllowedArguments = ["@CustomerCode"],
            AllowAdditionalArguments = false,
            Arguments =
            [
                new CapabilityArgumentRule
                {
                    Name = "@CustomerCode",
                    Type = "string",
                    MinLength = 1
                }
            ]
        }
    };

    private static CapabilityDescriptor WriteExecuteCapability() => new()
    {
        CapabilityKey = "sales.order.create-execute",
        Domain = "sales",
        AdapterType = "sql",
        Operation = "write_execute",
        TargetSystemId = "sql",
        ExecutionMode = "write_execute",
        RequiredRoles = ["sales.write"],
        IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storedProcedure"] = "dbo.sales_order_create"
        },
        ArgumentContract = new CapabilityArgumentContract
        {
            RequiredArguments = ["@CustomerCode"],
            AllowedArguments = ["@CustomerCode"],
            AllowAdditionalArguments = false,
            Arguments =
            [
                new CapabilityArgumentRule
                {
                    Name = "@CustomerCode",
                    Type = "string",
                    MinLength = 1
                }
            ]
        }
    };

    private static TilsoftExecutionContext Context(IReadOnlyList<string> roles) => new()
    {
        TenantId = "tenant-a",
        UserId = "user-a",
        Roles = roles.ToArray(),
        CorrelationId = Guid.NewGuid().ToString("N"),
        Language = "en-US"
    };

    private sealed class RecordingAdapter : IToolAdapter
    {
        private readonly ToolExecutionResult _result;

        public RecordingAdapter(ToolExecutionResult result)
        {
            _result = result;
        }

        public string AdapterType => "sql";
        public int Calls { get; private set; }
        public ToolExecutionRequest? LastRequest { get; private set; }

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken ct)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class SingleAdapterRegistry : IToolAdapterRegistry
    {
        private readonly IToolAdapter _adapter;

        public SingleAdapterRegistry(IToolAdapter adapter)
        {
            _adapter = adapter;
        }

        public IToolAdapter Resolve(string adapterType) => _adapter;

        public IToolAdapter ResolveForCapability(string capabilityKey, string systemId) => _adapter;
    }

    private sealed class SingleCapabilityRegistry : ICapabilityRegistry
    {
        private readonly CapabilityDescriptor _capability;

        public SingleCapabilityRegistry(CapabilityDescriptor capability)
        {
            _capability = capability;
        }

        public IReadOnlyList<CapabilityDescriptor> GetAll() => [_capability];

        public IReadOnlyList<CapabilityDescriptor> GetByDomain(string domain) =>
            string.Equals(_capability.Domain, domain, StringComparison.OrdinalIgnoreCase)
                ? [_capability]
                : [];

        public CapabilityDescriptor? Resolve(string capabilityKey) =>
            string.Equals(_capability.CapabilityKey, capabilityKey, StringComparison.OrdinalIgnoreCase)
                ? _capability
                : null;
    }

    private sealed class FixedExecutionContextAccessor : IExecutionContextAccessor
    {
        public FixedExecutionContextAccessor(TilsoftExecutionContext current)
        {
            Current = current;
        }

        public TilsoftExecutionContext Current { get; }
    }

    private sealed class RecordingApprovalEngine : IApprovalEngine
    {
        public List<ProposedAction> CreatedActions { get; } = [];
        public List<string> ExecutedActionIds { get; } = [];
        public string? ExpectedPayloadJson { get; private set; }

        public Task<ProposedActionRecord> CreateAsync(
            ProposedAction action,
            ApprovalContext context,
            CancellationToken ct)
        {
            CreatedActions.Add(action);
            return Task.FromResult(new ProposedActionRecord
            {
                ActionId = "action-1",
                TenantId = context.TenantId,
                ConversationId = context.ConversationId,
                RequestedAtUtc = DateTime.UtcNow,
                Status = "Pending",
                ActionType = action.ActionType,
                AgentId = action.AgentId,
                TargetSystem = action.TargetSystem,
                CapabilityKey = action.CapabilityKey,
                ToolName = action.ToolName,
                StoredProcedure = action.StoredProcedure,
                PayloadJson = action.PayloadJson,
                DiffPreviewJson = action.DiffPreviewJson,
                RiskLevel = action.RiskLevel,
                ApprovalRequirement = action.ApprovalRequirement,
                RequestedByUserId = context.UserId
            });
        }

        public Task<ProposedActionRecord> ApproveAsync(string actionId, ApprovalContext context, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ProposedActionRecord> RejectAsync(string actionId, ApprovalContext context, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ActionExecutionResult> ExecuteAsync(string actionId, ApprovalContext context, CancellationToken ct)
        {
            ExecutedActionIds.Add(actionId);
            return Task.FromResult(new ActionExecutionResult
            {
                Action = new ProposedActionRecord
                {
                    ActionId = actionId,
                    TenantId = context.TenantId,
                    Status = "Executed",
                    CapabilityKey = "sales.order.create-execute",
                    RequestedByUserId = context.UserId
                },
                RawResult = """{"ok":true}""",
                CompactedResult = """{"ok":true}"""
            });
        }

        public Task<ActionExecutionResult> ExecuteAsync(
            string actionId,
            ApprovalContext context,
            string? expectedPayloadJson,
            CancellationToken ct)
        {
            ExpectedPayloadJson = expectedPayloadJson;
            return ExecuteAsync(actionId, context, ct);
        }
    }
}
