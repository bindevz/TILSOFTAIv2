using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Analytics;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Analytics;

/// <summary>
/// Assembles query results into structured insight output.
/// PATCH 28: Complete output contract with warnings/freshness in notes.
/// </summary>
public sealed class InsightAssemblyService : IInsightAssemblyService
{
    private readonly AnalyticsOptions _options;
    private readonly ILogger<InsightAssemblyService> _logger;

    public InsightAssemblyService(
        IOptions<AnalyticsOptions> options,
        ILogger<InsightAssemblyService> logger)
    {
        _options = options?.Value ?? new AnalyticsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<InsightOutput> AssembleAsync(
        TaskFrame taskFrame,
        IReadOnlyList<QueryResultSet> queryResults,
        TilsoftExecutionContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(taskFrame);
        ArgumentNullException.ThrowIfNull(queryResults);
        ArgumentNullException.ThrowIfNull(context);

        var warnings = CollectWarnings(queryResults);
        var freshness = GetFreshness(queryResults);

        var insight = new InsightOutput
        {
            Headline = BuildHeadline(taskFrame, queryResults, context.Language),
            Tables = BuildTables(queryResults, context.Language),
            Notes = BuildNotes(taskFrame, queryResults, warnings, freshness, context.Language),
            Warnings = warnings,
            Freshness = freshness
        };

        _logger.LogInformation(
            "InsightAssembled | Headline: {Headline} | Tables: {TableCount} | Notes: {NoteCount} | Warnings: {WarningCount}",
            insight.Headline.Text, insight.Tables.Count, insight.Notes.Count, insight.Warnings.Count);

        return Task.FromResult(insight);
    }

    private static InsightHeadline BuildHeadline(
        TaskFrame taskFrame, 
        IReadOnlyList<QueryResultSet> results,
        string language)
    {
        var totalResult = results.FirstOrDefault(r => r.Type == QueryResultType.Total);
        
        string headlineText;
        if (totalResult?.Rows.Count > 0 && totalResult.Rows[0].Count > 0)
        {
            var value = totalResult.Rows[0][0];
            var formattedValue = FormatNumber(value);
            
            var entity = taskFrame.Entity ?? "items";
            var filterContext = BuildFilterContext(taskFrame, language);
            
            headlineText = language == "vi"
                ? $"{filterContext} có {formattedValue} {entity}"
                : $"{filterContext} has {formattedValue} {entity}";
        }
        else
        {
            // Fallback: sum from breakdown if no total
            var breakdown = results.FirstOrDefault(r => r.Type == QueryResultType.Breakdown);
            if (breakdown?.Rows.Count > 0)
            {
                var sum = breakdown.Rows
                    .Where(r => r.Count > 1)
                    .Sum(r => Convert.ToDecimal(r[^1] ?? 0));
                
                var entity = taskFrame.Entity ?? "items";
                var filterContext = BuildFilterContext(taskFrame, language);
                headlineText = language == "vi"
                    ? $"{filterContext} có {FormatNumber(sum)} {entity}"
                    : $"{filterContext} has {FormatNumber(sum)} {entity}";
            }
            else
            {
                headlineText = language == "vi" 
                    ? "Không có dữ liệu" 
                    : "No data available";
            }
        }

        return new InsightHeadline { Text = headlineText, Language = language };
    }

    private static string BuildFilterContext(TaskFrame taskFrame, string language)
    {
        if (taskFrame.Filters.Count == 0)
            return language == "vi" ? "Tổng cộng" : "Overall";

        var seasonFilter = taskFrame.Filters
            .FirstOrDefault(f => f.FieldHint?.Contains("season", StringComparison.OrdinalIgnoreCase) == true);
        
        if (seasonFilter?.Value != null)
        {
            return language == "vi" 
                ? $"Mùa {seasonFilter.Value}" 
                : $"Season {seasonFilter.Value}";
        }

        return language == "vi" ? "Theo bộ lọc" : "Filtered";
    }

    private static List<InsightTable> BuildTables(
        IReadOnlyList<QueryResultSet> results, 
        string language)
    {
        return results
            .Where(r => r.Type == QueryResultType.Breakdown && r.Rows.Count > 0)
            .Select(r => new InsightTable
            {
                Title = r.Label,
                Columns = r.Columns,
                Rows = r.Rows,
                TopN = r.Rows.Count
            })
            .ToList();
    }

    /// <summary>
    /// Builds complete notes including filter, limit, warnings, and data freshness.
    /// PATCH 28: Now includes warnings and freshness as per enterprise output contract.
    /// </summary>
    private static List<string> BuildNotes(
        TaskFrame taskFrame,
        IReadOnlyList<QueryResultSet> results,
        List<string> warnings,
        DataFreshness? freshness,
        string language)
    {
        var notes = new List<string>();

        // 1. Filter notes
        if (taskFrame.Filters.Count > 0)
        {
            foreach (var filter in taskFrame.Filters)
            {
                var filterText = language == "vi"
                    ? $"Bộ lọc: {filter.FieldHint} {filter.Op} {filter.Value}"
                    : $"Filter: {filter.FieldHint} {filter.Op} {filter.Value}";
                notes.Add(filterText);
            }
        }
        else
        {
            notes.Add(language == "vi" ? "Bộ lọc: (không có)" : "Filter: (none)");
        }

        // 2. Limit/truncation notes
        var truncatedResults = results.Where(r => r.Type == QueryResultType.Breakdown && r.Truncated).ToList();
        if (truncatedResults.Count > 0)
        {
            foreach (var result in truncatedResults)
            {
                var limitText = language == "vi"
                    ? $"Giới hạn: Top {result.RowCount} cho {result.Label}"
                    : $"Limit: Top {result.RowCount} for {result.Label}";
                notes.Add(limitText);
            }
        }
        else
        {
            var maxRows = results.Where(r => r.Type == QueryResultType.Breakdown).MaxBy(r => r.RowCount)?.RowCount ?? 0;
            if (maxRows > 0)
            {
                notes.Add(language == "vi" 
                    ? $"Giới hạn: Hiển thị tối đa {maxRows} hàng" 
                    : $"Limit: Showing up to {maxRows} rows");
            }
        }

        // 3. Warnings notes
        if (warnings.Count > 0)
        {
            var warningsText = language == "vi"
                ? $"Cảnh báo: {string.Join("; ", warnings)}"
                : $"Warnings: {string.Join("; ", warnings)}";
            notes.Add(warningsText);
        }
        else
        {
            notes.Add(language == "vi" ? "Cảnh báo: (không có)" : "Warnings: (none)");
        }

        // 4. Data freshness notes
        if (freshness != null)
        {
            var freshnessText = language == "vi"
                ? $"Dữ liệu mới nhất: {freshness.AsOfUtc:yyyy-MM-dd HH:mm:ss} UTC, nguồn: {freshness.Source}"
                : $"Data freshness: {freshness.AsOfUtc:yyyy-MM-dd HH:mm:ss} UTC, source: {freshness.Source}";
            notes.Add(freshnessText);
        }

        return notes;
    }

    private static List<string> CollectWarnings(IReadOnlyList<QueryResultSet> results)
    {
        return results
            .SelectMany(r => r.Warnings)
            .Distinct()
            .ToList();
    }

    private static DataFreshness? GetFreshness(IReadOnlyList<QueryResultSet> results)
    {
        var latestResult = results
            .OrderByDescending(r => r.GeneratedAtUtc)
            .FirstOrDefault();

        return latestResult != null
            ? new DataFreshness
            {
                AsOfUtc = latestResult.GeneratedAtUtc,
                Source = "SQL"
            }
            : null;
    }

    private static string FormatNumber(object? value)
    {
        return value switch
        {
            null => "0",
            int i => i.ToString("N0", CultureInfo.InvariantCulture),
            long l => l.ToString("N0", CultureInfo.InvariantCulture),
            decimal d => d.ToString("N0", CultureInfo.InvariantCulture),
            double db => db.ToString("N0", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "0"
        };
    }
}
