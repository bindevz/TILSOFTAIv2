using System.Collections.Concurrent;
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

namespace TILSOFTAI.IntegrationTests.Approvals;

/// <summary>
/// Integration tests for approval lifecycle with realistic persistence boundary.
/// Uses InMemoryActionRequestStore that mimics real SQL store behavior (ID generation,
/// status transitions, timestamps) to validate the full create, approve, execute flow.
/// </summary>
public sealed class ApprovalLifecycleIntegrationTests
{
    /// <summary>
    /// In-memory implementation of IActionRequestStore that mimics real SQL behavior.
    /// Generates unique IDs, enforces status transitions, and tracks timestamps.
    /// </summary>
    private sealed class InMemoryActionRequestStore : IActionRequestStore
    {
        private readonly ConcurrentDictionary<string, ActionRequestRecord> _store = new();
        private int _sequence;

        public Task<ActionRequestRecord> CreateAsync(ActionRequestCreateRequest request, CancellationToken ct)
        {
            var record = ActionRequestRecord.FromCreateRequest(request, DateTime.UtcNow);
            var id = $"action-{Interlocked.Increment(ref _sequence):D6}";
            record.ActionId = id;
            record.RequestedAtUtc = DateTime.UtcNow;
            record.ExpiresAtUtc = record.ExpiresAtUtc == default ? DateTime.UtcNow.AddHours(24) : record.ExpiresAtUtc;
            _store[id] = record;
            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord?> GetAsync(string tenantId, string actionId, CancellationToken ct)
        {
            _store.TryGetValue(actionId, out var record);
            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord?> GetActiveForConversationAsync(
            string tenantId,
            string userId,
            string conversationId,
            CancellationToken ct)
        {
            var record = _store.Values
                .Where(r => r.TenantId == tenantId)
                .Where(r => r.UserId == userId || r.RequestedByUserId == userId)
                .Where(r => r.ConversationId == conversationId)
                .Where(r => r.Status == ActionRequestStatus.Pending)
                .OrderByDescending(r => r.RequestedAtUtc)
                .FirstOrDefault();

            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord> ConfirmAsync(string tenantId, string userId, string actionId, CancellationToken ct)
        {
            if (!_store.TryGetValue(actionId, out var record))
                throw new InvalidOperationException("Action not found.");
            if (record.TenantId != tenantId)
                throw new InvalidOperationException("Action not found.");
            if (record.UserId != userId && record.RequestedByUserId != userId)
                throw new InvalidOperationException("Action not found.");
            if (record.Status != ActionRequestStatus.Pending)
                throw new InvalidOperationException("Action must be Pending to confirm.");
            if (record.IsExpired(DateTime.UtcNow))
                throw new InvalidOperationException("Action has expired.");

            record.Status = ActionRequestStatus.Confirmed;
            record.ConfirmedAtUtc = DateTime.UtcNow;
            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord> RejectAsync(
            string tenantId,
            string userId,
            string actionId,
            string? reason,
            CancellationToken ct)
        {
            if (!_store.TryGetValue(actionId, out var record))
                throw new InvalidOperationException("Action not found.");
            record.Status = "Rejected";
            record.ApprovedByUserId = userId;
            record.ApprovedAtUtc = DateTime.UtcNow;
            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord> MarkExecutedAsync(
            string tenantId,
            string actionId,
            string executedByUserId,
            string? resultCompactJson,
            bool success,
            CancellationToken ct)
        {
            if (!_store.TryGetValue(actionId, out var record))
                throw new InvalidOperationException("Action not found.");
            record.Status = success ? ActionRequestStatus.Executed : ActionRequestStatus.Failed;
            record.ExecutedByUserId = executedByUserId;
            record.ExecutedAtUtc = DateTime.UtcNow;
            record.ExecutionResultCompactJson = resultCompactJson;
            return Task.FromResult(record);
        }

        public Task<int> ExpireOldAsync(DateTimeOffset nowUtc, CancellationToken ct)
        {
            var count = 0;
            foreach (var record in _store.Values)
            {
                if (record.Status != ActionRequestStatus.Pending || !record.IsExpired(nowUtc.UtcDateTime))
                {
                    continue;
                }

                record.Status = ActionRequestStatus.Expired;
                count++;
            }

            return Task.FromResult(count);
        }
    }

    private static (ApprovalEngine engine, InMemoryActionRequestStore store) BuildEngine()
    {
        var store = new InMemoryActionRequestStore();

        var stubAdapter = new Mock<IToolAdapter>();
        stubAdapter.Setup(a => a.AdapterType).Returns("sql");

        // Catalog check: return a valid catalog entry
        stubAdapter.Setup(a => a.ExecuteAsync(
                It.Is<ToolExecutionRequest>(r => r.Operation == "execute_query" && r.Metadata.ContainsKey("storedProcedure") &&
                    r.Metadata["storedProcedure"] == "dbo.app_writeactioncatalog_get"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Ok(
                "[{\"ActionName\":\"test_write\",\"RequiredRoles\":\"ai_user\",\"JsonSchema\":null}]"));

        // Write execution: succeed
        stubAdapter.Setup(a => a.ExecuteAsync(
                It.Is<ToolExecutionRequest>(r => r.Operation == "execute_write_action"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Ok("{\"affected\": 1}"));

        var adapterRegistry = new ToolAdapterRegistry(new[] { stubAdapter.Object });

        var chatOptions = Options.Create(new ChatOptions
        {
            CompactionLimits = new Dictionary<string, int> { ["ToolResultMaxBytes"] = 16000 },
            CompactionRules = new CompactionRules()
        });

        var schemaValidator = new Mock<IJsonSchemaValidator>();
        schemaValidator.Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new JsonSchemaValidationResult(true, Array.Empty<string>(), null));

        var conversationStore = new Mock<IConversationStore>();
        conversationStore.Setup(s => s.SaveToolExecutionAsync(
                It.IsAny<TilsoftExecutionContext>(),
                It.IsAny<ToolExecutionRecord>(),
                It.IsAny<RequestPolicy>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var engine = new ApprovalEngine(
            store,
            adapterRegistry,
            new ToolResultCompactor(),
            conversationStore.Object,
            chatOptions,
            schemaValidator.Object,
            new Mock<ILogger<ApprovalEngine>>().Object);

        return (engine, store);
    }

    [Fact]
    public async Task FullLifecycle_Create_Approve_Execute_ShouldSucceed()
    {
        var (engine, store) = BuildEngine();

        var context = new ApprovalContext
        {
            TenantId = "tenant-lifecycle",
            ConversationId = "conv-1",
            UserId = "user-1",
            Roles = new[] { "ai_user" },
            CorrelationId = "corr-lifecycle"
        };

        // Step 1: Create
        var proposed = new ProposedAction
        {
            StoredProcedure = "app_test_write",
            ToolName = "test.write",
            CapabilityKey = "test.write",
            ActionType = "write",
            TargetSystem = "sql",
            PayloadJson = "{\"value\": 42}",
            AgentId = "test-agent"
        };

        var created = await engine.CreateAsync(proposed, context, CancellationToken.None);

        created.ActionId.Should().NotBeNullOrWhiteSpace();
        created.Status.Should().Be("Pending");

        // Step 2: Approve
        var approved = await engine.ApproveAsync(created.ActionId, context, CancellationToken.None);

        approved.Status.Should().Be("Confirmed");

        // Step 3: Execute
        var executed = await engine.ExecuteAsync(created.ActionId, context, CancellationToken.None);

        executed.Action.Status.Should().Be("Executed");
        executed.RawResult.Should().Contain("affected");
    }

    [Fact]
    public async Task Rejected_Action_ShouldNotExecute()
    {
        var (engine, _) = BuildEngine();

        var context = new ApprovalContext
        {
            TenantId = "tenant-reject",
            ConversationId = "conv-2",
            UserId = "user-2",
            Roles = new[] { "ai_user" },
            CorrelationId = "corr-reject"
        };

        var proposed = new ProposedAction
        {
            StoredProcedure = "app_test_write",
            ToolName = "test.write",
            CapabilityKey = "test.write",
            ActionType = "write",
            TargetSystem = "sql",
            PayloadJson = "{\"value\": 99}",
            AgentId = "test-agent"
        };

        var created = await engine.CreateAsync(proposed, context, CancellationToken.None);
        await engine.RejectAsync(created.ActionId, context, CancellationToken.None);

        var act = () => engine.ExecuteAsync(created.ActionId, context, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be*Approved*");
    }
}
