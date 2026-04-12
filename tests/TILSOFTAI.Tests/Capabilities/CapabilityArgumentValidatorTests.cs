using System.Text.Json;
using FluentAssertions;
using TILSOFTAI.Orchestration.Capabilities;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class CapabilityArgumentValidatorTests
{
    [Fact]
    public void Validate_ShouldRejectInvalidStringType()
    {
        var capability = WarehouseCapabilities.All.Single(c => c.CapabilityKey == "warehouse.inventory.by-item");

        var result = CapabilityArgumentValidator.Validate(capability, "{\"@ItemNo\":123}");

        result.IsValid.Should().BeFalse();
        DetailJson(result).Should().Contain("invalid_argument_type");
        DetailJson(result).Should().Contain("ItemNo");
    }

    [Fact]
    public void Validate_ShouldRejectInvalidCurrencyFormat()
    {
        var capability = AccountingCapabilities.All.Single(c => c.CapabilityKey == "accounting.exchange-rate.lookup");

        var result = CapabilityArgumentValidator.Validate(capability, "{\"@CurrencyCode\":\"usd\"}");

        result.IsValid.Should().BeFalse();
        DetailJson(result).Should().Contain("invalid_argument_format");
        DetailJson(result).Should().Contain("currency-code");
    }

    [Fact]
    public void Validate_ShouldRejectCurrencyOutsideAllowedEnum()
    {
        var capability = AccountingCapabilities.All.Single(c => c.CapabilityKey == "accounting.exchange-rate.lookup");

        var result = CapabilityArgumentValidator.Validate(capability, "{\"@CurrencyCode\":\"JPY\"}");

        result.IsValid.Should().BeFalse();
        DetailJson(result).Should().Contain("invalid_argument_enum");
    }

    [Fact]
    public void Validate_ShouldAcceptTypedRepresentativeArguments()
    {
        var warehouse = WarehouseCapabilities.All.Single(c => c.CapabilityKey == "warehouse.inventory.by-item");
        var accounting = AccountingCapabilities.All.Single(c => c.CapabilityKey == "accounting.exchange-rate.lookup");

        CapabilityArgumentValidator.Validate(warehouse, "{\"@ItemNo\":\"CHAIR-001\",\"@TenantId\":\"tenant-1\"}")
            .IsValid.Should().BeTrue();
        CapabilityArgumentValidator.Validate(accounting, "{\"@CurrencyCode\":\"USD\"}")
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldRejectUnexpectedArgumentsForNoArgumentContract()
    {
        var capability = WarehouseCapabilities.All.Single(c => c.CapabilityKey == "warehouse.inventory.summary");

        var result = CapabilityArgumentValidator.Validate(capability, "{\"@ItemNo\":\"CHAIR-001\"}");

        result.IsValid.Should().BeFalse();
        DetailJson(result).Should().Contain("unexpected_arguments");
        CapabilityArgumentValidator.Validate(capability, "{}").IsValid.Should().BeTrue();
    }

    private static string DetailJson(CapabilityArgumentValidationResult result) =>
        JsonSerializer.Serialize(result.Detail);
}
