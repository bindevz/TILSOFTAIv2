using FluentAssertions;
using Moq;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Actions;
using Xunit;

namespace TILSOFTAI.Tests.Actions;

public sealed class PendingActionConfirmationResolverTests
{
    private const string TenantId = "tenant-a";
    private const string UserId = "user-a";
    private const string ConversationId = "conversation-a";

    [Theory]
    [InlineData("confirm")]
    [InlineData("yes")]
    [InlineData("xác nhận")]
    [InlineData("đồng ý")]
    public async Task TryResolveAsync_ShouldConfirmActivePendingAction_WithoutExecuting(string input)
    {
        var pending = CreatePendingAction();
        var confirmed = CreatePendingAction();
        confirmed.Status = ActionRequestStatus.Confirmed;
        confirmed.ConfirmedAtUtc = DateTime.UtcNow;

        var store = new Mock<IActionRequestStore>(MockBehavior.Strict);
        store.Setup(s => s.ExpireOldAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        store.Setup(s => s.GetActiveForConversationAsync(TenantId, UserId, ConversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        store.Setup(s => s.ConfirmAsync(TenantId, UserId, pending.ActionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmed);

        var resolver = new PendingActionConfirmationResolver(store.Object);

        var result = await resolver.TryResolveAsync(input, CreateContext(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Handled.Should().BeTrue();
        result.Answer.Should().NotBeNull();
        result.Answer!.AnswerType.Should().Be("confirmation_ready");
        result.Answer.Text.Should().Contain("no write has been executed");
        store.Verify(
            s => s.MarkExecutedAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryResolveAsync_ShouldReturnFollowUp_WhenNoPendingActionExists()
    {
        var store = new Mock<IActionRequestStore>(MockBehavior.Strict);
        store.Setup(s => s.ExpireOldAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        store.Setup(s => s.GetActiveForConversationAsync(TenantId, UserId, ConversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionRequestRecord?)null);

        var resolver = new PendingActionConfirmationResolver(store.Object);

        var result = await resolver.TryResolveAsync("confirm", CreateContext(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Handled.Should().BeTrue();
        result.Answer.Should().NotBeNull();
        result.Answer!.AnswerType.Should().Be("follow_up");
        result.Answer.Detail.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["reason"] = "no_pending_action"
        });
    }

    [Fact]
    public async Task TryResolveAsync_ShouldIgnoreNonConfirmationMessage()
    {
        var store = new Mock<IActionRequestStore>(MockBehavior.Strict);
        var resolver = new PendingActionConfirmationResolver(store.Object);

        var result = await resolver.TryResolveAsync("show inventory", CreateContext(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryResolveAsync_ShouldNotConfirmAnotherUsersPendingAction()
    {
        var store = new InMemoryActionRequestStore();
        await store.CreateAsync(new ActionRequestCreateRequest
        {
            TenantId = TenantId,
            UserId = "user-a",
            ConversationId = ConversationId,
            CapabilityKey = "model.update",
            FunctionName = "model_update",
            ProposedToolName = "model.update",
            ProposedProcedureName = "dbo.app_model_update",
            ProposedArgumentsJson = "{}",
            RequestedByUserId = "user-a",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        }, CancellationToken.None);
        var resolver = new PendingActionConfirmationResolver(store);

        var result = await resolver.TryResolveAsync(
            "confirm",
            CreateContext("user-b"),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Answer!.AnswerType.Should().Be("follow_up");
        store.Records.Should().ContainSingle()
            .Which.Status.Should().Be(ActionRequestStatus.Pending);
    }

    [Fact]
    public async Task TryResolveAsync_ShouldNotConfirmExpiredPendingAction()
    {
        var store = new InMemoryActionRequestStore();
        await store.CreateAsync(new ActionRequestCreateRequest
        {
            TenantId = TenantId,
            UserId = UserId,
            ConversationId = ConversationId,
            CapabilityKey = "model.update",
            FunctionName = "model_update",
            ProposedToolName = "model.update",
            ProposedProcedureName = "dbo.app_model_update",
            ProposedArgumentsJson = "{}",
            RequestedByUserId = UserId,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
        }, CancellationToken.None);
        var resolver = new PendingActionConfirmationResolver(store);

        var result = await resolver.TryResolveAsync("đồng ý", CreateContext(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Answer!.AnswerType.Should().Be("follow_up");
        store.Records.Should().ContainSingle()
            .Which.Status.Should().Be(ActionRequestStatus.Expired);
    }

    private static TilsoftExecutionContext CreateContext(string? userId = null) => new()
    {
        TenantId = TenantId,
        UserId = userId ?? UserId,
        ConversationId = ConversationId,
        CorrelationId = "corr-a"
    };

    private static ActionRequestRecord CreatePendingAction() => new()
    {
        ActionId = "action-a",
        TenantId = TenantId,
        UserId = UserId,
        ConversationId = ConversationId,
        CapabilityKey = "model.update",
        FunctionName = "model_update",
        ProposedToolName = "model.update",
        ProposedProcedureName = "dbo.app_model_update",
        ProposedArgumentsJson = "{\"modelCode\":\"M001\"}",
        PreviewResultJson = "{\"preview\":true}",
        Status = ActionRequestStatus.Pending,
        RequestedByUserId = UserId,
        RequestedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
        CorrelationId = "corr-a"
    };

    private sealed class InMemoryActionRequestStore : IActionRequestStore
    {
        private readonly Dictionary<string, ActionRequestRecord> _records = new(StringComparer.OrdinalIgnoreCase);
        private int _sequence;

        public IReadOnlyCollection<ActionRequestRecord> Records => _records.Values;

        public Task<ActionRequestRecord> CreateAsync(ActionRequestCreateRequest request, CancellationToken cancellationToken)
        {
            var record = ActionRequestRecord.FromCreateRequest(request, DateTime.UtcNow);
            return CreateAsync(record, cancellationToken);
        }

        public Task<ActionRequestRecord> CreateAsync(ActionRequestRecord request, CancellationToken cancellationToken)
        {
            request.ActionId = string.IsNullOrWhiteSpace(request.ActionId)
                ? $"action-{Interlocked.Increment(ref _sequence)}"
                : request.ActionId;
            _records[request.ActionId] = request;
            return Task.FromResult(request);
        }

        public Task<ActionRequestRecord?> GetAsync(string tenantId, string actionId, CancellationToken cancellationToken)
        {
            _records.TryGetValue(actionId, out var record);
            return Task.FromResult(record is not null
                && string.Equals(record.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                    ? record
                    : null);
        }

        public Task<ActionRequestRecord?> GetActiveForConversationAsync(
            string tenantId,
            string userId,
            string conversationId,
            CancellationToken cancellationToken)
        {
            var record = _records.Values.FirstOrDefault(item =>
                string.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.UserId, userId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.ConversationId, conversationId, StringComparison.OrdinalIgnoreCase)
                && ActionRequestStatus.IsActivePending(item.Status)
                && !item.IsExpired(DateTime.UtcNow));
            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord> ConfirmAsync(
            string tenantId,
            string userId,
            string actionId,
            CancellationToken cancellationToken)
        {
            var record = _records[actionId];
            if (string.Equals(record.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.UserId, userId, StringComparison.OrdinalIgnoreCase)
                && ActionRequestStatus.IsActivePending(record.Status)
                && !record.IsExpired(DateTime.UtcNow))
            {
                record.Status = ActionRequestStatus.Confirmed;
                record.ConfirmedAtUtc = DateTime.UtcNow;
            }

            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord> RejectAsync(
            string tenantId,
            string userId,
            string actionId,
            string? reason,
            CancellationToken cancellationToken)
        {
            var record = _records[actionId];
            record.Status = ActionRequestStatus.Rejected;
            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord> MarkExecutedAsync(
            string tenantId,
            string actionId,
            string executedByUserId,
            CancellationToken cancellationToken)
        {
            var record = _records[actionId];
            if (string.Equals(record.Status, ActionRequestStatus.Approved, StringComparison.OrdinalIgnoreCase)
                && !record.IsExpired(DateTime.UtcNow))
            {
                record.Status = ActionRequestStatus.Executed;
                record.ExecutedAtUtc = DateTime.UtcNow;
                record.ExecutedByUserId = executedByUserId;
            }

            return Task.FromResult(record);
        }

        public Task<int> ExpireOldAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            var count = 0;
            foreach (var record in _records.Values)
            {
                if (ActionRequestStatus.IsActivePending(record.Status)
                    && record.ExpiresAtUtc <= nowUtc.UtcDateTime)
                {
                    record.Status = ActionRequestStatus.Expired;
                    count++;
                }
            }

            return Task.FromResult(count);
        }

        public Task<ActionRequestRecord> ApproveAsync(
            string tenantId,
            string actionId,
            string approvedByUserId,
            CancellationToken cancellationToken)
        {
            var record = _records[actionId];
            record.Status = ActionRequestStatus.Approved;
            record.ApprovedByUserId = approvedByUserId;
            record.ApprovedAtUtc = DateTime.UtcNow;
            return Task.FromResult(record);
        }

        public Task<ActionRequestRecord> MarkExecutedAsync(
            string tenantId,
            string actionId,
            string resultCompactJson,
            bool success,
            CancellationToken cancellationToken)
        {
            var record = _records[actionId];
            record.Status = success ? ActionRequestStatus.Executed : ActionRequestStatus.Failed;
            record.ExecutedAtUtc = DateTime.UtcNow;
            return Task.FromResult(record);
        }
    }
}
