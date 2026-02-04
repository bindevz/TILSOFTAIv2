using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Analytics;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Analytics;

/// <summary>
/// Assembles query results into structured insight output.
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

        var insight = new InsightOutput
        {
            Headline = BuildHeadline(taskFrame, queryResults, context.Language),
            Tables = BuildTables(queryResults, context.Language),
            Notes = BuildNotes(taskFrame, queryResults, context.Language),
            Warnings = CollectWarnings(queryResults),
            Freshness = GetFreshness(queryResults)
        };

        _logger.LogInformation(
            "InsightAssembled | Headline: {Headline} | Tables: {TableCount} | Notes: {NoteCount}",
            insight.Headline.Text, insight.Tables.Count, insight.Notes.Count);

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
                headlineText = language == "vi"
                    ? $"Tổng: {FormatNumber(sum)} {entity}"
                    : $"Total: {FormatNumber(sum)} {entity}";
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

    private static List<string> BuildNotes(
        TaskFrame taskFrame,
        IReadOnlyList<QueryResultSet> results,
        string language)
    {
        var notes = new List<string>();

        // Filter notes
        foreach (var filter in taskFrame.Filters)
        {
            var filterText = language == "vi"
                ? $"Bộ lọc: {filter.FieldHint} {filter.Op} {filter.Value}"
                : $"Filter: {filter.FieldHint} {filter.Op} {filter.Value}";
            notes.Add(filterText);
        }

        // Limit notes
        foreach (var result in results.Where(r => r.Type == QueryResultType.Breakdown))
        {
            if (result.Truncated)
            {
                var limitText = language == "vi"
                    ? $"Giới hạn: Top {result.RowCount} cho {result.Label}"
                    : $"Limit: Top {result.RowCount} for {result.Label}";
                notes.Add(limitText);
            }
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
