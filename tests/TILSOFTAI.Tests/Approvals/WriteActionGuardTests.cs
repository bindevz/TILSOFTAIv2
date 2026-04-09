using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TILSOFTAI.Approvals;
using TILSOFTAI.Orchestration.Actions;
using Xunit;

namespace TILSOFTAI.Tests.Approvals;

public sealed class WriteActionGuardTests
{
    [Fact]
    public async Task ValidateAsync_ShouldRejectWhenActionNotFound()
    {
        var store = new Mock<IActionRequestStore>();
        store.Setup(x => x.GetAsync("tenant-a", "missing-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionRequestRecord?)null);
        var logger = new Mock<ILogger<ApprovalBackedWriteActionGuard>>();

        var guard = new ApprovalBackedWriteActionGuard(store.Object, logger.Object);

        var result = await guard.ValidateAsync("tenant-a", "missing-id", CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Reason.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectWhenNotApproved()
    {
        var store = new Mock<IActionRequestStore>();
        store.Setup(x => x.GetAsync("tenant-a", "action-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionRequestRecord
            {
                ActionId = "action-1",
                TenantId = "tenant-a",
                Status = "Pending"
            });
        var logger = new Mock<ILogger<ApprovalBackedWriteActionGuard>>();

        var guard = new ApprovalBackedWriteActionGuard(store.Object, logger.Object);

        var result = await guard.ValidateAsync("tenant-a", "action-1", CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Reason.Should().Contain("Pending").And.Contain("must be 'Approved'");
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectWhenAlreadyExecuted()
    {
        var store = new Mock<IActionRequestStore>();
        store.Setup(x => x.GetAsync("tenant-a", "action-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionRequestRecord
            {
                ActionId = "action-1",
                TenantId = "tenant-a",
                Status = "Approved",
                ApprovedByUserId = "approver-a",
                ExecutedAtUtc = DateTime.UtcNow
            });
        var logger = new Mock<ILogger<ApprovalBackedWriteActionGuard>>();

        var guard = new ApprovalBackedWriteActionGuard(store.Object, logger.Object);

        var result = await guard.ValidateAsync("tenant-a", "action-1", CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Reason.Should().Contain("already been executed");
    }

    [Fact]
    public async Task ValidateAsync_ShouldApproveWhenValid()
    {
        var store = new Mock<IActionRequestStore>();
        store.Setup(x => x.GetAsync("tenant-a", "action-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionRequestRecord
            {
                ActionId = "action-1",
                TenantId = "tenant-a",
                Status = "Approved",
                ApprovedByUserId = "approver-a"
            });
        var logger = new Mock<ILogger<ApprovalBackedWriteActionGuard>>();

        var guard = new ApprovalBackedWriteActionGuard(store.Object, logger.Object);

        var result = await guard.ValidateAsync("tenant-a", "action-1", CancellationToken.None);

        result.IsApproved.Should().BeTrue();
        result.ApprovedByUserId.Should().Be("approver-a");
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectEmptyTenantId()
    {
        var store = new Mock<IActionRequestStore>();
        var logger = new Mock<ILogger<ApprovalBackedWriteActionGuard>>();

        var guard = new ApprovalBackedWriteActionGuard(store.Object, logger.Object);

        var result = await guard.ValidateAsync("", "action-1", CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Reason.Should().Contain("TenantId");
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectEmptyActionId()
    {
        var store = new Mock<IActionRequestStore>();
        var logger = new Mock<ILogger<ApprovalBackedWriteActionGuard>>();

        var guard = new ApprovalBackedWriteActionGuard(store.Object, logger.Object);

        var result = await guard.ValidateAsync("tenant-a", "", CancellationToken.None);

        result.IsApproved.Should().BeFalse();
        result.Reason.Should().Contain("ActionId");
    }
}
