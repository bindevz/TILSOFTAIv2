using System.Globalization;
using System.Text;
using TILSOFTAI.Domain.Analytics;

namespace TILSOFTAI.Orchestration.Analytics;

/// <summary>
/// Renders InsightOutput to stable markdown format.
/// PATCH 29.02: Deterministic stable rendering with headline + tables + notes.
/// </summary>
public sealed class InsightRenderer
{
    private static readonly CultureInfo FormatCulture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Renders insight to markdown format.
    /// </summary>
    public string Render(InsightOutput insight, string language = "en")
    {
        ArgumentNullException.ThrowIfNull(insight);

        var sb = new StringBuilder();

        // Line 1: Headline (exactly one line, no prose before)
        sb.AppendLine(insight.Headline.Text);
        sb.AppendLine();

        // Tables (markdown tables)
        foreach (var table in insight.Tables)
        {
            RenderTable(sb, table);
            sb.AppendLine();
        }

        // Notes section
        if (insight.Notes.Count > 0)
        {
            var notesHeader = language == "vi" ? "**Ghi chú:**" : "**Notes:**";
            sb.AppendLine(notesHeader);
            foreach (var note in insight.Notes)
            {
                sb.AppendLine($"- {note}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private void RenderTable(StringBuilder sb, InsightTable table)
    {
        if (table.Rows.Count == 0)
            return;

        // Table title
        if (!string.IsNullOrWhiteSpace(table.Title))
        {
            sb.AppendLine($"### {table.Title}");
            sb.AppendLine();
        }

        // Header row
        var headerRow = string.Join(" | ", table.Columns);
        sb.AppendLine($"| {headerRow} |");

        // Separator row
        var separator = string.Join(" | ", table.Columns.Select(_ => "---"));
        sb.AppendLine($"| {separator} |");

        // Data rows
        foreach (var row in table.Rows)
        {
            var formattedCells = row.Select(FormatCell);
            var dataRow = string.Join(" | ", formattedCells);
            sb.AppendLine($"| {dataRow} |");
        }
    }

    private static string FormatCell(object? value)
    {
        return value switch
        {
            null => "-",
            int i => i.ToString("N0", FormatCulture),
            long l => l.ToString("N0", FormatCulture),
            decimal d when d == Math.Floor(d) => d.ToString("N0", FormatCulture),
            decimal d => d.ToString("N2", FormatCulture),
            double db when db == Math.Floor(db) => db.ToString("N0", FormatCulture),
            double db => db.ToString("N2", FormatCulture),
            float f when f == MathF.Floor(f) => f.ToString("N0", FormatCulture),
            float f => f.ToString("N2", FormatCulture),
            DateTime dt => dt.ToString("yyyy-MM-dd", FormatCulture),
            _ => value.ToString() ?? "-"
        };
    }
}
