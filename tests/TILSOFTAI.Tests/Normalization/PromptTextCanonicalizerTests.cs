using FluentAssertions;
using TILSOFTAI.Orchestration.Normalization;
using Xunit;

namespace TILSOFTAI.Tests.Normalization;

/// <summary>
/// Unit tests for PromptTextCanonicalizer.
/// PATCH 29.09: Tests for NBSP/tabs/multi-spaces/CRLF normalization.
/// </summary>
public class PromptTextCanonicalizerTests
{
    #region Line Ending Normalization

    [Theory]
    [InlineData("hello\r\nworld", "hello\nworld")]
    [InlineData("hello\rworld", "hello\nworld")]
    [InlineData("hello\nworld", "hello\nworld")]
    [InlineData("a\r\nb\rc\nd", "a\nb\nc\nd")]
    public void Canonicalize_LineEndings_ShouldNormalizeToLF(string input, string expected)
    {
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be(expected);
    }

    #endregion

    #region Whitespace Collapsing

    [Theory]
    [InlineData("hello  world", "hello world")]
    [InlineData("hello   world", "hello world")]
    [InlineData("hello    world", "hello world")]
    public void Canonicalize_MultipleSpaces_ShouldCollapseToSingle(string input, string expected)
    {
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void Canonicalize_Tabs_ShouldConvertToSpaces()
    {
        var input = "hello\tworld";
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be("hello world");
    }

    [Fact]
    public void Canonicalize_Nbsp_ShouldConvertToSpaces()
    {
        var input = "hello\u00A0world"; // NBSP
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be("hello world");
    }

    [Fact]
    public void Canonicalize_MixedWhitespace_ShouldNormalize()
    {
        var input = "hello\t\u00A0  world"; // Tab + NBSP + spaces
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be("hello world");
    }

    #endregion

    #region Line Trimming

    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("  hello\n  world  ", "hello\nworld")]
    [InlineData("\t\thello\t\t", "hello")]
    public void Canonicalize_LeadingTrailingWhitespace_ShouldTrim(string input, string expected)
    {
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void Canonicalize_IndentedLines_ShouldTrimEachLine()
    {
        var input = "  line1  \n  line2  \n  line3  ";
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be("line1\nline2\nline3");
    }

    #endregion

    #region Blank Line Collapsing

    [Fact]
    public void Canonicalize_ExcessiveBlankLines_ShouldCollapseToOne()
    {
        var input = "hello\n\n\n\nworld";
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be("hello\n\nworld");
    }

    [Fact]
    public void Canonicalize_SingleBlankLine_ShouldPreserve()
    {
        var input = "hello\n\nworld";
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be("hello\n\nworld");
    }

    [Fact]
    public void Canonicalize_ManyBlankLines_ShouldCollapseToMax()
    {
        var input = "a\n\n\n\n\n\nb\n\n\n\nc";
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be("a\n\nb\n\nc");
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\t\t\t", "")]
    [InlineData("\n\n\n", "")]
    public void Canonicalize_EmptyOrWhitespaceOnly_ShouldReturnEmpty(string? input, string expected)
    {
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void Canonicalize_ComplexInput_ShouldNormalizeCompletely()
    {
        var input = "  hello\r\n\t\t  world  \r\n\r\n\r\n  foo  \u00A0  bar  ";
        var result = PromptTextCanonicalizer.Canonicalize(input);
        result.Should().Be("hello\nworld\n\nfoo bar");
    }

    #endregion

    #region WouldBeEmpty Tests

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("\t\n\r", true)]
    [InlineData("hello", false)]
    [InlineData("  hello  ", false)]
    [InlineData("123", false)]
    public void WouldBeEmpty_ShouldDetectEmptyResults(string? input, bool expected)
    {
        var result = PromptTextCanonicalizer.WouldBeEmpty(input);
        result.Should().Be(expected);
    }

    #endregion
}
