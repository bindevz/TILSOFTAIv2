using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Caching;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Normalization;
using Xunit;

namespace TILSOFTAI.Tests.Normalization;

/// <summary>
/// PATCH 33.02: Tests for NormalizationService safety filter.
/// Verifies that unsafe whitespace-stripping rules are skipped.
/// </summary>
public class NormalizationServiceSafetyTests
{
    private readonly Mock<INormalizationRuleProvider> _ruleProviderMock;
    private readonly Mock<IRedisCacheProvider> _cacheProviderMock;
    private readonly NormalizationService _service;
    private readonly TilsoftExecutionContext _ctx;

    public NormalizationServiceSafetyTests()
    {
        _ruleProviderMock = new Mock<INormalizationRuleProvider>();
        _cacheProviderMock = new Mock<IRedisCacheProvider>();

        var redisOptions = Options.Create(new RedisOptions
        {
            Enabled = false,
            DefaultTtlMinutes = 30
        });

        _service = new NormalizationService(
            _ruleProviderMock.Object,
            _cacheProviderMock.Object,
            redisOptions,
            NullLogger<NormalizationService>.Instance);

        _ctx = new TilsoftExecutionContext
        {
            TenantId = "test-tenant",
            UserId = "test-user",
            ConversationId = Guid.NewGuid().ToString()
        };
    }

    [Fact]
    public async Task NormalizeAsync_UnsafeWhitespaceStripRule_ShouldBeIgnored()
    {
        // Arrange: A bad rule that strips all whitespace
        var rules = new List<NormalizationRuleRecord>
        {
            new("strip_spaces", 1, @"\s+", "")
        };
        _ruleProviderMock
            .Setup(r => r.GetRulesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var input = "phân tích model với ID là 3";

        // Act
        var result = await _service.NormalizeAsync(input, _ctx, CancellationToken.None);

        // Assert: spaces must be preserved (>= 2 tokens)
        result.Should().Contain(" ");
        var tokens = result.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        tokens.Length.Should().BeGreaterOrEqualTo(2, "text must retain word boundaries");
    }

    [Fact]
    public async Task NormalizeAsync_SafeRule_ShouldBeApplied()
    {
        // Arrange: A safe rule that replaces digits
        var rules = new List<NormalizationRuleRecord>
        {
            new("replace_digits", 1, @"\d+", "NUM")
        };
        _ruleProviderMock
            .Setup(r => r.GetRulesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var input = "model 123";

        // Act
        var result = await _service.NormalizeAsync(input, _ctx, CancellationToken.None);

        // Assert: digits should be replaced
        result.Should().Contain("NUM");
        result.Should().NotContain("123");
    }

    [Fact]
    public async Task NormalizeAsync_MixedRules_SkipsOnlyUnsafe()
    {
        // Arrange: one safe rule + one unsafe rule
        var rules = new List<NormalizationRuleRecord>
        {
            new("safe_rule", 1, @"\btest\b", "TEST"),
            new("unsafe_whitespace", 2, @"\s+", "")
        };
        _ruleProviderMock
            .Setup(r => r.GetRulesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var input = "test input sentence";

        // Act
        var result = await _service.NormalizeAsync(input, _ctx, CancellationToken.None);

        // Assert: safe rule applied, unsafe skipped
        result.Should().Contain("TEST");
        result.Should().Contain(" "); // spaces preserved
    }

    [Theory]
    [InlineData(@"\s+", "", true)]
    [InlineData(@"[\s]+", "", true)]
    [InlineData(@"\p{Z}+", "", true)]
    [InlineData(@"\t", "", true)]
    [InlineData(@"\r\n", "", true)]
    [InlineData(@"\d+", "", false)]      // Not whitespace-related
    [InlineData(@"\s+", " ", false)]     // Non-empty replacement is safe
    [InlineData(@"hello", "", false)]    // No whitespace pattern
    public void IsUnsafeWhitespaceRule_ShouldClassifyCorrectly(string pattern, string replacement, bool expectedUnsafe)
    {
        var rule = new NormalizationRuleRecord("test", 1, pattern, replacement);
        NormalizationService.IsUnsafeWhitespaceRule(rule).Should().Be(expectedUnsafe);
    }

    [Theory]
    [InlineData("hello world", 2)]
    [InlineData("one", 1)]
    [InlineData("a b c d", 4)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("  spaced  out  ", 2)]
    public void TokenCount_ShouldReturnExpected(string? text, int expectedCount)
    {
        NormalizationService.TokenCount(text).Should().Be(expectedCount);
    }
}
