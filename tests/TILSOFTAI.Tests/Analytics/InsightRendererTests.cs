using FluentAssertions;
using TILSOFTAI.Domain.Analytics;
using TILSOFTAI.Orchestration.Analytics;
using Xunit;

namespace TILSOFTAI.Tests.Analytics;

/// <summary>
/// Unit tests for InsightRenderer.
/// PATCH 29.09: Tests for headline single-line and notes with freshness/warnings.
/// </summary>
public class InsightRendererTests
{
    private readonly InsightRenderer _renderer = new();

    #region Headline Tests

    [Fact]
    public void Render_HeadlineShouldBeSingleLine()
    {
        var insight = CreateBasicInsight("Total: 150 models for season 25/26");

        var result = _renderer.Render(insight);

        var firstLine = result.Split('\n')[0];
        firstLine.Should().Be("Total: 150 models for season 25/26");
        firstLine.Should().NotContain("\r");
    }

    [Fact]
    public void Render_HeadlineShouldBeFirstLine()
    {
        var insight = CreateBasicInsight("Test headline");

        var result = _renderer.Render(insight);

        result.Should().StartWith("Test headline");
    }

    [Fact]
    public void Render_HeadlineFollowedByBlankLine()
    {
        var insight = CreateBasicInsight("Headline text");
        insight.Tables.Add(new InsightTable
        {
            Columns = new List<string> { "A" },
            Rows = new List<List<object?>> { new() { "1" } }
        });

        var result = _renderer.Render(insight);

        result.Should().Contain("Headline text\n\n");
    }

    #endregion

    #region Table Rendering Tests

    [Fact]
    public void Render_TableWithTitle_ShouldIncludeHeader()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Tables.Add(new InsightTable
        {
            Title = "Breakdown by Category",
            Columns = new List<string> { "Category", "Count" },
            Rows = new List<List<object?>>
            {
                new() { "A", 10 },
                new() { "B", 20 }
            }
        });

        var result = _renderer.Render(insight);

        result.Should().Contain("### Breakdown by Category");
        result.Should().Contain("| Category | Count |");
        result.Should().Contain("| --- | --- |");
        result.Should().Contain("| A | 10 |");
        result.Should().Contain("| B | 20 |");
    }

    [Fact]
    public void Render_EmptyTable_ShouldSkip()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Tables.Add(new InsightTable
        {
            Title = "Empty Table",
            Columns = new List<string> { "A" },
            Rows = new List<List<object?>>()
        });

        var result = _renderer.Render(insight);

        result.Should().NotContain("### Empty Table");
    }

    [Fact]
    public void Render_NumericValues_ShouldFormatWithThousandsSeparator()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Tables.Add(new InsightTable
        {
            Columns = new List<string> { "Value" },
            Rows = new List<List<object?>>
            {
                new() { 1234567 }
            }
        });

        var result = _renderer.Render(insight);

        result.Should().Contain("1,234,567");
    }

    [Fact]
    public void Render_DecimalValues_ShouldFormatWithTwoDecimals()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Tables.Add(new InsightTable
        {
            Columns = new List<string> { "Value" },
            Rows = new List<List<object?>>
            {
                new() { 123.456m }
            }
        });

        var result = _renderer.Render(insight);

        result.Should().Contain("123.46");
    }

    [Fact]
    public void Render_NullValues_ShouldRenderAsDash()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Tables.Add(new InsightTable
        {
            Columns = new List<string> { "Value" },
            Rows = new List<List<object?>>
            {
                new() { null }
            }
        });

        var result = _renderer.Render(insight);

        result.Should().Contain("| - |");
    }

    #endregion

    #region Notes Section Tests

    [Fact]
    public void Render_WithNotes_ShouldIncludeNotesHeader()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Notes.Add("Filter: season = 25/26");

        var result = _renderer.Render(insight);

        result.Should().Contain("**Notes:**");
        result.Should().Contain("- Filter: season = 25/26");
    }

    [Fact]
    public void Render_VietnameseLanguage_ShouldUseViNotesHeader()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Notes.Add("Lọc: mùa = 25/26");

        var result = _renderer.Render(insight, "vi");

        result.Should().Contain("**Ghi chú:**");
    }

    [Fact]
    public void Render_WithFreshnessNote_ShouldIncludeInNotes()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Notes.Add("Data as of: 2026-02-05 12:00 UTC");

        var result = _renderer.Render(insight);

        result.Should().Contain("Data as of: 2026-02-05");
    }

    [Fact]
    public void Render_WithWarningNote_ShouldIncludeInNotes()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Notes.Add("Warning: Results truncated to 200 rows");

        var result = _renderer.Render(insight);

        result.Should().Contain("Warning: Results truncated");
    }

    [Fact]
    public void Render_WithLimitNote_ShouldIncludeInNotes()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Notes.Add("Limit: 10 rows shown");

        var result = _renderer.Render(insight);

        result.Should().Contain("Limit: 10 rows");
    }

    [Fact]
    public void Render_MultipleNotes_ShouldIncludeAll()
    {
        var insight = CreateBasicInsight("Headline");
        insight.Notes.Add("Filter: season = 25/26");
        insight.Notes.Add("Limit: 10 rows shown");
        insight.Notes.Add("Data as of: 2026-02-05");

        var result = _renderer.Render(insight);

        result.Should().Contain("- Filter: season = 25/26");
        result.Should().Contain("- Limit: 10 rows shown");
        result.Should().Contain("- Data as of: 2026-02-05");
    }

    [Fact]
    public void Render_NoNotes_ShouldNotIncludeNotesSection()
    {
        var insight = CreateBasicInsight("Headline");

        var result = _renderer.Render(insight);

        result.Should().NotContain("**Notes:**");
        result.Should().NotContain("**Ghi chú:**");
    }

    #endregion

    #region Helper Methods

    private static InsightOutput CreateBasicInsight(string headline)
    {
        return new InsightOutput
        {
            Headline = new InsightHeadline { Text = headline },
            Tables = new List<InsightTable>(),
            Notes = new List<string>(),
            Freshness = new DataFreshness { AsOfUtc = DateTime.UtcNow }
        };
    }

    #endregion
}
