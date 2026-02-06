using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Analytics;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Analytics;

/// <summary>
/// Assembles query results into structured insight output.
/// PATCH 28: Complete output contract with warnings/freshness in notes.
/// PATCH 30.05: Build notes from validatedPlan for fidelity.
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
        string? validatedPlanJson,
        TilsoftExecutionContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(taskFrame);
        ArgumentNullException.ThrowIfNull(queryResults);
        ArgumentNullException.ThrowIfNull(context);

        var warnings = CollectWarnings(queryResults);
        var freshness = GetFreshness(queryResults);

        // PATCH 30.05: Parse validated plan for accurate notes
        var planSummary = ParsePlanSummary(validatedPlanJson);

        var insight = new InsightOutput
        {
            Headline = BuildHeadline(taskFrame, queryResults, context.Language),
            Tables = BuildTables(queryResults, context.Language),
            Notes = BuildNotesFromPlan(planSummary, queryResults, warnings, freshness, context.Language),
            Warnings = warnings,
            Freshness = freshness
        };

        _logger.LogInformation(
            "InsightAssembled | Headline: {Headline} | Tables: {TableCount} | Notes: {NoteCount} | Warnings: {WarningCount}",
            insight.Headline.Text, insight.Tables.Count, insight.Notes.Count, insight.Warnings.Count);

        return Task.FromResult(insight);
    }

    /// <summary>
    /// PATCH 30.05: Parse validated plan JSON to extract filters, limits, and groupBy.
    /// </summary>
    private static ExecutedPlanSummary ParsePlanSummary(string? planJson)
    {
        if (string.IsNullOrWhiteSpace(planJson))
            return new ExecutedPlanSummary();

        try
        {
            using var doc = JsonDocument.Parse(planJson);
            var root = doc.RootElement;
            var summary = new ExecutedPlanSummary();

            // Extract where clauses (filters)
            if (root.TryGetProperty("where", out var where) && where.ValueKind == JsonValueKind.Array)
            {
                foreach (var filter in where.EnumerateArray())
                {
                    var field = filter.TryGetProperty("field", out var f) ? f.GetString() : null;
                    var op = filter.TryGetProperty("op", out var o) ? o.GetString() : "=";
                    var value = filter.TryGetProperty("value", out var v) ? GetJsonValue(v) : null;
                    
                    if (!string.IsNullOrEmpty(field))
                    {
                        summary.Filters.Add(new PlanFilter
                        {
                            Field = field,
                            Op = op ?? "=",
                            Value = NormalizeFilterValue(field, value)
                        });
                    }
                }
            }

            // Extract groupBy
            if (root.TryGetProperty("groupBy", out var groupBy) && groupBy.ValueKind == JsonValueKind.Array)
            {
                foreach (var gb in groupBy.EnumerateArray())
                {
                    if (gb.ValueKind == JsonValueKind.String)
                        summary.GroupBy.Add(gb.GetString()!);
                }
            }

            // Extract topN/limit
            if (root.TryGetProperty("topN", out var topN))
                summary.TopN = topN.GetInt32();
            else if (root.TryGetProperty("limit", out var limit))
                summary.TopN = limit.GetInt32();

            // Extract metrics
            if (root.TryGetProperty("metrics", out var metrics) && metrics.ValueKind == JsonValueKind.Array)
            {
                foreach (var metric in metrics.EnumerateArray())
                {
                    var op = metric.TryGetProperty("op", out var mo) ? mo.GetString() : null;
                    var field = metric.TryGetProperty("field", out var mf) ? mf.GetString() : null;
                    if (!string.IsNullOrEmpty(op))
                        summary.Metrics.Add($"{op}({field ?? "*"})");
                }
            }

            return summary;
        }
        catch (JsonException)
        {
            return new ExecutedPlanSummary();
        }
    }

    private static string? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// PATCH 30.05: Normalize filter values (e.g., season "mùa 25/26" -> "2025/2026").
    /// </summary>
    private static string? NormalizeFilterValue(string field, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Normalize season formats
        if (field.Contains("season", StringComparison.OrdinalIgnoreCase))
        {
            // Convert "25/26" -> "2025/2026"
            var match = System.Text.RegularExpressions.Regex.Match(value, @"(\d{2})/(\d{2})$");
            if (match.Success)
            {
                var year1 = int.Parse(match.Groups[1].Value);
                var year2 = int.Parse(match.Groups[2].Value);
                // Assume 2000s for 2-digit years
                year1 = year1 < 50 ? 2000 + year1 : 1900 + year1;
                year2 = year2 < 50 ? 2000 + year2 : 1900 + year2;
                return $"{year1}/{year2}";
            }
        }

        return value;
    }

    /// <summary>
    /// PATCH 30.05: Build notes from executed plan for fidelity.
    /// </summary>
    private static List<string> BuildNotesFromPlan(
        ExecutedPlanSummary planSummary,
        IReadOnlyList<QueryResultSet> results,
        List<string> warnings,
        DataFreshness? freshness,
        string language)
    {
        var notes = new List<string>();

        // 1. Filter notes from actual plan.where
        if (planSummary.Filters.Count > 0)
        {
            foreach (var filter in planSummary.Filters)
            {
                var filterText = language == "vi"
                    ? $"Bộ lọc: {filter.Field} {filter.Op} {filter.Value}"
                    : $"Filter: {filter.Field} {filter.Op} {filter.Value}";
                notes.Add(filterText);
            }
        }
        else
        {
            notes.Add(language == "vi" ? "Bộ lọc: (không có)" : "Filter: (none)");
        }

        // 2. Limit/topN notes from plan
        if (planSummary.TopN > 0)
        {
            notes.Add(language == "vi" 
                ? $"Giới hạn: Top {planSummary.TopN}" 
                : $"Limit: Top {planSummary.TopN}");
        }

        // 3. Truncation notes from execution meta
        var truncatedResults = results.Where(r => r.Type == QueryResultType.Breakdown && r.Truncated).ToList();
        if (truncatedResults.Count > 0)
        {
            foreach (var result in truncatedResults)
            {
                var truncText = language == "vi"
                    ? $"Đã cắt bớt: {result.Label} chỉ hiển thị {result.RowCount} hàng"
                    : $"Truncated: {result.Label} showing {result.RowCount} rows";
                notes.Add(truncText);
            }
        }

        // 4. Warnings notes (mandatory)
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

        // 5. Data freshness notes (mandatory)
        if (freshness != null)
        {
            var freshnessText = language == "vi"
                ? $"Dữ liệu mới nhất: {freshness.AsOfUtc:yyyy-MM-dd HH:mm:ss} UTC"
                : $"Data freshness: {freshness.AsOfUtc:yyyy-MM-dd HH:mm:ss} UTC";
            notes.Add(freshnessText);
        }
        else
        {
            notes.Add(language == "vi" ? "Dữ liệu mới nhất: (không xác định)" : "Data freshness: (unknown)");
        }

        return notes;
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

/// <summary>
/// PATCH 30.05: Summary of executed plan for accurate notes.
/// </summary>
public sealed class ExecutedPlanSummary
{
    public List<PlanFilter> Filters { get; set; } = new();
    public List<string> GroupBy { get; set; } = new();
    public List<string> Metrics { get; set; } = new();
    public int TopN { get; set; }
}

/// <summary>
/// PATCH 30.05: Filter from validated plan.
/// </summary>
public sealed class PlanFilter
{
    public string Field { get; set; } = string.Empty;
    public string Op { get; set; } = "=";
    public string? Value { get; set; }
}
