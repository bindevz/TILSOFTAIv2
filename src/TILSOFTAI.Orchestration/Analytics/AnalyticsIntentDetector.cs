using System.Text.RegularExpressions;

namespace TILSOFTAI.Orchestration.Analytics;

/// <summary>
/// Detects analytics intent from user queries using deterministic rules.
/// PATCH 29.02: Rules-first intent detection for Vietnamese and English.
/// </summary>
public sealed class AnalyticsIntentDetector
{
    // Vietnamese analytics triggers
    private static readonly string[] ViMetricTriggers = 
    {
        "bao nhiêu", "có bao nhiêu", "tổng cộng", "tổng số",
        "đếm", "tính tổng", "trung bình",
        "phân bổ", "phân tích", "thống kê",
        "top", "cao nhất", "thấp nhất", "nhiều nhất", "ít nhất"
    };

    // English analytics triggers
    private static readonly string[] EnMetricTriggers =
    {
        "how many", "how much", "total", "count",
        "sum of", "average", "avg",
        "breakdown", "distribution", "statistics", "analyze",
        "top", "highest", "lowest", "most", "least"
    };

    // Common entity hints (fashion industry context)
    private static readonly string[] EntityHints =
    {
        "model", "mẫu", "style", "kiểu",
        "order", "đơn hàng", "đơn", 
        "product", "sản phẩm", "hàng",
        "customer", "khách hàng", "khách",
        "supplier", "nhà cung cấp",
        "collection", "bộ sưu tập"
    };

    // Season patterns (e.g., "25/26", "2025", "mùa 25")
    private static readonly Regex SeasonPattern = new(
        @"\b(mùa|season|ss|fw|aw)?\s*(\d{2}[/-]\d{2}|\d{4})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Date patterns
    private static readonly Regex DatePattern = new(
        @"\b(\d{1,2}[/-]\d{1,2}[/-]?\d{2,4}|\d{4}[/-]\d{1,2}[/-]\d{1,2})\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Detects analytics intent from user input.
    /// </summary>
    public AnalyticsIntentResult Detect(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new AnalyticsIntentResult { IsAnalytics = false, Confidence = 0 };
        }

        var normalized = input.ToLowerInvariant().Trim();
        var hints = new List<string>();
        decimal confidence = 0;

        // Check metric triggers (required for analytics)
        var hasViTrigger = ViMetricTriggers.Any(t => normalized.Contains(t));
        var hasEnTrigger = EnMetricTriggers.Any(t => normalized.Contains(t));
        var hasMetricTrigger = hasViTrigger || hasEnTrigger;

        if (!hasMetricTrigger)
        {
            return new AnalyticsIntentResult { IsAnalytics = false, Confidence = 0 };
        }

        // Base confidence from metric trigger
        confidence = 0.4m;
        
        if (hasViTrigger)
            hints.Add("metric_trigger_vi");
        if (hasEnTrigger)
            hints.Add("metric_trigger_en");

        // Check entity hints
        var matchedEntities = EntityHints
            .Where(e => normalized.Contains(e.ToLowerInvariant()))
            .ToList();
        
        if (matchedEntities.Count > 0)
        {
            confidence += 0.25m;
            hints.AddRange(matchedEntities.Select(e => $"entity:{e}"));
        }

        // Check season pattern
        var seasonMatch = SeasonPattern.Match(normalized);
        if (seasonMatch.Success)
        {
            confidence += 0.2m;
            hints.Add($"season:{seasonMatch.Value}");
        }

        // Check date pattern
        var dateMatch = DatePattern.Match(normalized);
        if (dateMatch.Success)
        {
            confidence += 0.15m;
            hints.Add($"date:{dateMatch.Value}");
        }

        // Require at least entity OR season/date for bounded false positives
        var hasContextualHint = matchedEntities.Count > 0 || seasonMatch.Success || dateMatch.Success;
        
        if (!hasContextualHint)
        {
            // Reduce confidence significantly without context
            confidence *= 0.5m;
        }

        // Threshold check
        var isAnalytics = confidence >= 0.5m;

        return new AnalyticsIntentResult
        {
            IsAnalytics = isAnalytics,
            Confidence = Math.Min(confidence, 1.0m),
            Hints = hints,
            DetectedLanguage = hasViTrigger && !hasEnTrigger ? "vi" : "en"
        };
    }
}

/// <summary>
/// Result of analytics intent detection.
/// </summary>
public sealed class AnalyticsIntentResult
{
    public bool IsAnalytics { get; set; }
    public decimal Confidence { get; set; }
    public List<string> Hints { get; set; } = new();
    public string DetectedLanguage { get; set; } = "en";
}
