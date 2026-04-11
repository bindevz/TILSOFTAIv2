using FluentAssertions;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Capabilities;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class CapabilityAccessPolicyTests
{
    [Fact]
    public void Evaluate_ShouldAllow_WhenRequiredRolePresent()
    {
        var capability = new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.inventory.summary",
            RequiredRoles = new[] { "warehouse_read" }
        };

        var decision = CapabilityAccessPolicy.Evaluate(capability, new TilsoftExecutionContext
        {
            Roles = new[] { "warehouse_read" }
        });

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldDeny_WhenRequiredRoleMissing()
    {
        var capability = new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.inventory.summary",
            RequiredRoles = new[] { "warehouse_read" }
        };

        var decision = CapabilityAccessPolicy.Evaluate(capability, new TilsoftExecutionContext
        {
            Roles = new[] { "accounting_read" }
        });

        decision.Allowed.Should().BeFalse();
        decision.Code.Should().Be("CAPABILITY_ACCESS_DENIED");
    }

    [Fact]
    public void Evaluate_ShouldDeny_WhenTenantNotAllowed()
    {
        var capability = new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.inventory.summary",
            AllowedTenants = new[] { "tenant-a" }
        };

        var decision = CapabilityAccessPolicy.Evaluate(capability, new TilsoftExecutionContext
        {
            TenantId = "tenant-b"
        });

        decision.Allowed.Should().BeFalse();
        decision.Code.Should().Be("CAPABILITY_ACCESS_DENIED");
    }
}
