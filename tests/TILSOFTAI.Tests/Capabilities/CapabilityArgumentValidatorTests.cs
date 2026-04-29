using System.Text.Json;
using FluentAssertions;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Tests.Fixtures;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class CapabilityArgumentValidatorTests
{
    [Fact]
    public void Validate_ShouldRejectInvalidStringType()
    {
        var capability = ModelCapabilityFixtures.All.Single(c => c.CapabilityKey == "model.overview.by-code");

        var result = CapabilityArgumentValidator.Validate(capability, "{\"modelCode\":123}");

        result.IsValid.Should().BeFalse();
        DetailJson(result).Should().Contain("invalid_argument_type");
        DetailJson(result).Should().Contain("modelCode");
    }

    [Fact]
    public void Validate_ShouldRejectInvalidModelCodeFormat()
    {
        var capability = ModelCapabilityFixtures.All.Single(c => c.CapabilityKey == "model.overview.by-code");

        var result = CapabilityArgumentValidator.Validate(capability, "{\"modelCode\":\"bad code\"}");

        result.IsValid.Should().BeFalse();
        DetailJson(result).Should().Contain("invalid_argument_format");
        DetailJson(result).Should().Contain("model-code");
    }

    [Fact]
    public void Validate_ShouldRejectUnexpectedArguments()
    {
        var capability = ModelCapabilityFixtures.All.Single(c => c.CapabilityKey == "model.count");

        var result = CapabilityArgumentValidator.Validate(capability, "{\"modelCode\":\"CHAIR-001\"}");

        result.IsValid.Should().BeFalse();
        DetailJson(result).Should().Contain("unexpected_arguments");
    }

    [Fact]
    public void Validate_ShouldAcceptTypedRepresentativeArguments()
    {
        var byCode = ModelCapabilityFixtures.All.Single(c => c.CapabilityKey == "model.overview.by-code");
        var count = ModelCapabilityFixtures.All.Single(c => c.CapabilityKey == "model.count");

        CapabilityArgumentValidator.Validate(byCode, "{\"modelCode\":\"CHAIR-001\"}")
            .IsValid.Should().BeTrue();
        CapabilityArgumentValidator.Validate(count, "{\"season\":\"2026\"}")
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldRejectUnexpectedArgumentsForNoArgumentContract()
    {
        var capability = new CapabilityDescriptor
        {
            CapabilityKey = "model.synthetic",
            Domain = "model",
            AdapterType = "sql",
            Operation = "execute_tool",
            TargetSystemId = "sql",
            ExecutionMode = "readonly",
            ArgumentContract = new CapabilityArgumentContract
            {
                AllowAdditionalArguments = false
            }
        };

        var result = CapabilityArgumentValidator.Validate(capability, "{\"modelCode\":\"CHAIR-001\"}");

        result.IsValid.Should().BeFalse();
        DetailJson(result).Should().Contain("unexpected_arguments");
        CapabilityArgumentValidator.Validate(capability, "{}").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldApplyMinLengthToArrayArguments()
    {
        var capability = ModelCapabilityFixtures.All.Single(c => c.CapabilityKey == "model.compare");

        CapabilityArgumentValidator.Validate(capability, """{"modelCodes":["ABC","DEF"]}""")
            .IsValid.Should().BeTrue();

        var result = CapabilityArgumentValidator.Validate(capability, """{"modelCodes":["ABC"]}""");

        result.IsValid.Should().BeFalse();
        DetailJson(result).Should().Contain("invalid_argument_min_length");
        DetailJson(result).Should().Contain("modelCodes");
    }

    private static string DetailJson(CapabilityArgumentValidationResult result) =>
        JsonSerializer.Serialize(result.Detail);
}
