using FluentAssertions;
using Moq;
using TILSOFTAI.Approvals;
using TILSOFTAI.Infrastructure.Sql;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Tools.Abstractions;
using Xunit;

namespace TILSOFTAI.Tests.Approvals;

public sealed class SqlToolAdapterWriteGuardTests
{
    [Fact]
    public async Task ExecuteWriteAction_ShouldRejectWhenNoApprovedActionId()
    {
        var sqlExecutor = new Mock<ISqlExecutor>();
        var guard = new Mock<IWriteActionGuard>();

        var adapter = new SqlToolAdapter(sqlExecutor.Object, guard.Object);

        var result = await adapter.ExecuteAsync(new ToolExecutionRequest
        {
            TenantId = "tenant-a",
            AgentId = "test-agent",
            SystemId = "sql",
            CapabilityKey = "inventory.adjust",
            Operation = ToolAdapterOperationNames.ExecuteWriteAction,
            ArgumentsJson = "{}",
            Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "dbo.app_inventory_adjust"
            }
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("WRITE_ACTION_NOT_APPROVED");

        // SqlExecutor should never be called
        sqlExecutor.Verify(
            x => x.ExecuteWriteActionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteWriteAction_ShouldRejectWhenGuardRejects()
    {
        var sqlExecutor = new Mock<ISqlExecutor>();
        var guard = new Mock<IWriteActionGuard>();
        guard.Setup(x => x.ValidateAsync("tenant-a", "action-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(WriteActionGuardResult.Rejected("Action not approved"));

        var adapter = new SqlToolAdapter(sqlExecutor.Object, guard.Object);

        var result = await adapter.ExecuteAsync(new ToolExecutionRequest
        {
            TenantId = "tenant-a",
            AgentId = "test-agent",
            SystemId = "sql",
            CapabilityKey = "inventory.adjust",
            Operation = ToolAdapterOperationNames.ExecuteWriteAction,
            ArgumentsJson = "{}",
            Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "dbo.app_inventory_adjust",
                ["approvedActionId"] = "action-1"
            }
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("WRITE_ACTION_GUARD_REJECTED");

        sqlExecutor.Verify(
            x => x.ExecuteWriteActionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteWriteAction_ShouldProceedWhenGuardApproves()
    {
        var sqlExecutor = new Mock<ISqlExecutor>();
        sqlExecutor.Setup(x => x.ExecuteWriteActionAsync(
                "dbo.app_inventory_adjust", "tenant-a", "{\"qty\":5}", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"success\":true}");

        var guard = new Mock<IWriteActionGuard>();
        guard.Setup(x => x.ValidateAsync("tenant-a", "action-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(WriteActionGuardResult.Approved("approver-a"));

        var adapter = new SqlToolAdapter(sqlExecutor.Object, guard.Object);

        var result = await adapter.ExecuteAsync(new ToolExecutionRequest
        {
            TenantId = "tenant-a",
            AgentId = "test-agent",
            SystemId = "sql",
            CapabilityKey = "inventory.adjust",
            Operation = ToolAdapterOperationNames.ExecuteWriteAction,
            ArgumentsJson = "{\"qty\":5}",
            Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "dbo.app_inventory_adjust",
                ["approvedActionId"] = "action-1"
            }
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PayloadJson.Should().Be("{\"success\":true}");

        sqlExecutor.Verify(
            x => x.ExecuteWriteActionAsync("dbo.app_inventory_adjust", "tenant-a", "{\"qty\":5}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteReadTool_ShouldNotRequireApprovedActionId()
    {
        var sqlExecutor = new Mock<ISqlExecutor>();
        sqlExecutor.Setup(x => x.ExecuteToolAsync(
                "ai_inventory_list", "tenant-a", "{}", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"rows\":[]}");

        var guard = new Mock<IWriteActionGuard>();

        var adapter = new SqlToolAdapter(sqlExecutor.Object, guard.Object);

        var result = await adapter.ExecuteAsync(new ToolExecutionRequest
        {
            TenantId = "tenant-a",
            AgentId = "test-agent",
            SystemId = "sql",
            CapabilityKey = "inventory.list",
            Operation = ToolAdapterOperationNames.ExecuteTool,
            ArgumentsJson = "{}",
            Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "ai_inventory_list"
            }
        }, CancellationToken.None);

        result.Success.Should().BeTrue();

        // Guard should never be called for read operations
        guard.Verify(
            x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
