using FluentAssertions;
using Moq;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Actions;
using Xunit;

namespace TILSOFTAI.Tests.Approvals;

public sealed class ActionApprovalServiceFacadeTests
{
    [Fact]
    public async Task CreateAsync_ShouldMapLegacyArgumentsIntoProposedAction()
    {
        var approvalEngine = new Mock<IApprovalEngine>();
        approvalEngine.Setup(x => x.CreateAsync(
                It.IsAny<ProposedAction>(),
                It.IsAny<ApprovalContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProposedActionRecord
            {
                ActionId = "action-1",
                TenantId = "tenant-a",
                ConversationId = "conversation-a",
                Status = "Pending",
                CapabilityKey = "inventory.adjust",
                ToolName = "inventory.adjust",
                StoredProcedure = "dbo.app_inventory_adjust",
                PayloadJson = "{\"qty\":5}",
                RequestedByUserId = "user-a"
            });

        var facade = new ActionApprovalService(approvalEngine.Object);

        var result = await facade.CreateAsync(
            new TilsoftExecutionContext
            {
                TenantId = "tenant-a",
                UserId = "user-a",
                ConversationId = "conversation-a"
            },
            "inventory.adjust",
            "dbo.app_inventory_adjust",
            "{\"qty\":5}",
            CancellationToken.None);

        result.ActionId.Should().Be("action-1");
        result.ProposedToolName.Should().Be("inventory.adjust");
        result.ProposedSpName.Should().Be("dbo.app_inventory_adjust");

        approvalEngine.Verify(x => x.CreateAsync(
            It.Is<ProposedAction>(action =>
                action.CapabilityKey == "inventory.adjust"
                && action.StoredProcedure == "dbo.app_inventory_adjust"
                && action.PayloadJson == "{\"qty\":5}"),
            It.IsAny<ApprovalContext>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
