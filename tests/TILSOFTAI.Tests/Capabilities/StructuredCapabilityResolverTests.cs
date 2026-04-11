using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Tools.Abstractions;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class StructuredCapabilityResolverTests
{
    private static StructuredCapabilityResolver CreateResolver() =>
        new(new Mock<ILogger<StructuredCapabilityResolver>>().Object);

    private static IReadOnlyList<CapabilityDescriptor> AllCapabilities =>
        WarehouseCapabilities.All.Concat(AccountingCapabilities.All).ToList();

    // ────────────────────── Exact Key Match ──────────────────────

    [Theory]
    [InlineData("warehouse.inventory.summary")]
    [InlineData("accounting.receivables.summary")]
    [InlineData("accounting.invoice.by-number")]
    public void Resolve_ExactKey_ShouldMatchDirectly(string key)
    {
        var resolver = CreateResolver();
        var hint = new CapabilityRequestHint { CapabilityKey = key };

        var result = resolver.Resolve(hint, AllCapabilities);

        result.Should().NotBeNull();
        result!.CapabilityKey.Should().Be(key);
    }

    [Fact]
    public void Resolve_ExactKey_ShouldBeCaseInsensitive()
    {
        var resolver = CreateResolver();
        var hint = new CapabilityRequestHint { CapabilityKey = "WAREHOUSE.INVENTORY.SUMMARY" };

        var result = resolver.Resolve(hint, AllCapabilities);

        result.Should().NotBeNull();
        result!.CapabilityKey.Should().Be("warehouse.inventory.summary");
    }

    // ────────────────────── Keyword Matching ──────────────────────

    [Theory]
    [InlineData(new[] { "inventory", "summary" }, "warehouse.inventory.summary")]
    [InlineData(new[] { "receipts", "recent" }, "warehouse.receipts.recent")]
    [InlineData(new[] { "receivables", "summary" }, "accounting.receivables.summary")]
    [InlineData(new[] { "payables", "summary" }, "accounting.payables.summary")]
    [InlineData(new[] { "invoice", "number" }, "accounting.invoice.by-number")]
    public void Resolve_Keywords_ShouldMatchCapability(string[] keywords, string expectedKey)
    {
        var resolver = CreateResolver();
        var hint = new CapabilityRequestHint { SubjectKeywords = keywords };

        var result = resolver.Resolve(hint, AllCapabilities);

        result.Should().NotBeNull();
        result!.CapabilityKey.Should().Be(expectedKey);
    }

    [Fact]
    public void Resolve_Keywords_ShouldPreferHigherScoreMatch()
    {
        var resolver = CreateResolver();
        // "summary" matches both receivables and payables; "receivables" disambiguates
        var hint = new CapabilityRequestHint
        {
            SubjectKeywords = new[] { "receivables", "summary" }
        };

        var result = resolver.Resolve(hint, AccountingCapabilities.All);

        result.Should().NotBeNull();
        result!.CapabilityKey.Should().Be("accounting.receivables.summary");
    }

    // ────────────────────── No Match ──────────────────────

    [Fact]
    public void Resolve_UnknownKeywords_ShouldReturnNull()
    {
        var resolver = CreateResolver();
        var hint = new CapabilityRequestHint
        {
            SubjectKeywords = new[] { "weather", "temperature" }
        };

        var result = resolver.Resolve(hint, AllCapabilities);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_NullHint_ShouldReturnNull()
    {
        var resolver = CreateResolver();

        var result = resolver.Resolve(null!, AllCapabilities);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyCandidates_ShouldReturnNull()
    {
        var resolver = CreateResolver();
        var hint = new CapabilityRequestHint { CapabilityKey = "warehouse.inventory.summary" };

        var result = resolver.Resolve(hint, Array.Empty<CapabilityDescriptor>());

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyHint_ShouldReturnNull()
    {
        var resolver = CreateResolver();
        var hint = new CapabilityRequestHint();

        var result = resolver.Resolve(hint, AllCapabilities);

        result.Should().BeNull();
    }

    // ────────────────────── Priority ──────────────────────

    [Fact]
    public void Resolve_ExactKeyTakesPriority_OverKeywords()
    {
        var resolver = CreateResolver();
        // Exact key says payables, but keywords say receivables
        var hint = new CapabilityRequestHint
        {
            CapabilityKey = "accounting.payables.summary",
            SubjectKeywords = new[] { "receivables", "summary" }
        };

        var result = resolver.Resolve(hint, AccountingCapabilities.All);

        result.Should().NotBeNull();
        result!.CapabilityKey.Should().Be("accounting.payables.summary");
    }
}
