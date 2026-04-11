using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Orchestration.Llm;

namespace TILSOFTAI.Orchestration.Analytics;

/// <summary>
/// Detects analytics intent from user queries.
/// PATCH 29.02: Rules-first intent detection for Vietnamese and English.
/// PATCH 30.04: LLM-first classification with heuristic fallback.
/// </summary>
public sealed class AnalyticsIntentDetector
{
    private readonly ILlmClient? _llmClient;
    private readonly ILogger<AnalyticsIntentDetector>? _logger;

    // Confidence thresholds
    private const decimal RouteToAnalytics = 0.60m;
    private const decimal SkipAnalytics = 0.40m;

    // Vietnamese analytics triggers (soft hints, not hard gates)
    private static readonly string[] ViMetricTriggers = 
    {
        "bao nhiêu", "có bao nhiêu", "tổng cộng", "tổng số",
        "đếm", "tính tổng", "trung bình",
        "phân bổ", "phân tích", "thống kê",
        "top", "cao nhất", "thấp nhất", "nhiều nhất", "ít nhất"
    };

    // English analytics triggers (soft hints, not hard gates)
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

    // Obvious non-analytics patterns
    private static readonly string[] NonAnalyticsPatterns =
    {
        "xin chào", "chào", "hello", "hi", "hey",
        "cảm ơn", "thank you", "thanks",
        "tạm biệt", "goodbye", "bye",
        "viết code", "write code", "generate code",
        "viết bài", "write article", "compose"
    };

    private static readonly string[] ViMetricTriggersNormalized =
    {
        "bao nhieu", "co bao nhieu", "tong cong", "tong so",
        "dem", "tinh tong", "trung binh",
        "phan bo", "phan tich", "thong ke",
        "top", "cao nhat", "thap nhat", "nhieu nhat", "it nhat"
    };

    private static readonly string[] EntityHintsNormalized =
    {
        "model", "mau", "style", "kieu",
        "order", "don hang", "don",
        "product", "san pham", "hang",
        "customer", "khach hang", "khach",
        "supplier", "nha cung cap",
        "collection", "bo suu tap",
        "quantity", "quantities"
    };

    private static readonly string[] NonAnalyticsPatternsNormalized =
    {
        "xin chao", "chao", "hello", "hi", "hey",
        "cam on", "thank you", "thanks",
        "tam biet", "goodbye", "bye",
        "viet code", "write code", "generate code",
        "viet bai", "write article", "compose"
    };

    public AnalyticsIntentDetector()
    {
    }

    public AnalyticsIntentDetector(ILlmClient llmClient, ILogger<AnalyticsIntentDetector> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    /// <summary>
    /// PATCH 30.04: Async detection with LLM classification.
    /// Falls back to heuristic if LLM unavailable.
    /// </summary>
    public async Task<AnalyticsIntentResult> DetectAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new AnalyticsIntentResult { IsAnalytics = false, Confidence = 0 };
        }

        var normalized = NormalizeForRules(input);

        // Step 1: Lightweight pre-check for obvious non-analytics
        if (IsObviousNonAnalytics(normalized))
        {
            return new AnalyticsIntentResult 
            { 
                IsAnalytics = false, 
                Confidence = 0,
                Hints = new List<string> { "non_analytics_pattern" }
            };
        }

        // Step 2: Try LLM classification if available
        if (_llmClient != null)
        {
            try
            {
                var llmResult = await ClassifyWithLlmAsync(input, ct);
                if (llmResult != null)
                {
                    _logger?.LogDebug(
                        "LLM intent classification | IsAnalytics: {IsAnalytics} | Confidence: {Confidence}",
                        llmResult.IsAnalytics, llmResult.Confidence);
                    return llmResult;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "LLM intent classification failed, falling back to heuristic");
            }
        }

        // Step 3: Fallback to heuristic scoring
        return DetectHeuristic(normalized);
    }

    /// <summary>
    /// Synchronous detection using heuristic only (legacy compatibility).
    /// </summary>
    public AnalyticsIntentResult Detect(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new AnalyticsIntentResult { IsAnalytics = false, Confidence = 0 };
        }

        var normalized = NormalizeForRules(input);

        if (IsObviousNonAnalytics(normalized))
        {
            return new AnalyticsIntentResult 
            { 
                IsAnalytics = false, 
                Confidence = 0,
                Hints = new List<string> { "non_analytics_pattern" }
            };
        }

        return DetectHeuristic(normalized);
    }

    /// <summary>
    /// PATCH 30.04: LLM-based intent classification.
    /// Uses JSON-only output with temperature=0.
    /// </summary>
    private async Task<AnalyticsIntentResult?> ClassifyWithLlmAsync(string userText, CancellationToken ct)
    {
        if (_llmClient == null) return null;

        var systemPrompt = @"You are an intent classifier. Analyze if the user is asking for quantitative data analysis.
Return JSON only, no explanation:
{""isAnalytics"":true|false,""confidence"":0.0-1.0,""entityHint"":""...|null"",""breakdownHints"":[]}

Rules:
- isAnalytics=true if user wants counts, totals, averages, breakdowns, statistics, or comparisons of data
- isAnalytics=false if user wants general chat, code writing, creative writing, or non-data questions
- confidence: 0.0-1.0 based on how clear the intent is
- entityHint: main data entity if detected (model, order, product, etc.)
- breakdownHints: dimensions like season, customer, date if mentioned";

        var request = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            Messages = new List<LlmMessage>
            {
                new("user", userText)
            },
            MaxTokens = 100
        };

        var response = await _llmClient.CompleteAsync(request, ct);
        
        if (string.IsNullOrWhiteSpace(response?.Content))
            return null;

        // Parse JSON response
        try
        {
            var content = response.Content.Trim();
            // Handle markdown code blocks
            if (content.StartsWith("```"))
            {
                var startIdx = content.IndexOf('{');
                var endIdx = content.LastIndexOf('}');
                if (startIdx >= 0 && endIdx > startIdx)
                    content = content[startIdx..(endIdx + 1)];
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var isAnalytics = root.TryGetProperty("isAnalytics", out var ia) && ia.GetBoolean();
            var confidence = root.TryGetProperty("confidence", out var c) ? (decimal)c.GetDouble() : 0.5m;
            var entityHint = root.TryGetProperty("entityHint", out var e) && e.ValueKind == JsonValueKind.String 
                ? e.GetString() : null;

            var hints = new List<string> { "llm_classified" };
            if (!string.IsNullOrEmpty(entityHint))
                hints.Add($"entity:{entityHint}");

            if (root.TryGetProperty("breakdownHints", out var bh) && bh.ValueKind == JsonValueKind.Array)
            {
                foreach (var hint in bh.EnumerateArray())
                {
                    if (hint.ValueKind == JsonValueKind.String)
                        hints.Add($"breakdown:{hint.GetString()}");
                }
            }

            // Determine language from content
            var hasViPattern = ViMetricTriggersNormalized.Any(t => NormalizeForRules(userText).Contains(t));
            var language = hasViPattern ? "vi" : "en";

            // Apply thresholds
            var finalIsAnalytics = confidence >= RouteToAnalytics && isAnalytics;
            var isBorderline = confidence >= SkipAnalytics && confidence < RouteToAnalytics;

            if (isBorderline)
                hints.Add("borderline");

            return new AnalyticsIntentResult
            {
                IsAnalytics = finalIsAnalytics,
                IsBorderline = isBorderline,
                Confidence = confidence,
                Hints = hints,
                DetectedLanguage = language,
                EntityHint = entityHint
            };
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse LLM intent response: {Content}", response.Content);
            return null;
        }
    }

    /// <summary>
    /// Heuristic-based detection (soft scoring, no hard gates).
    /// PATCH 30.04: Triggers are hints, not gates.
    /// </summary>
    private AnalyticsIntentResult DetectHeuristic(string normalized)
    {
        var hints = new List<string>();
        decimal confidence = 0;

        // Check metric triggers (soft boost, not hard gate)
        var hasViTrigger = ViMetricTriggersNormalized.Any(t => normalized.Contains(t));
        var hasEnTrigger = EnMetricTriggers.Any(t => normalized.Contains(t));
        var hasMetricTrigger = hasViTrigger || hasEnTrigger;

        if (hasMetricTrigger)
        {
            confidence += 0.35m;
            if (hasViTrigger) hints.Add("metric_trigger_vi");
            if (hasEnTrigger) hints.Add("metric_trigger_en");
        }

        // Check entity hints
        var matchedEntities = EntityHintsNormalized
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
            confidence += 0.20m;
            hints.Add($"season:{seasonMatch.Value}");
        }

        // Check date pattern
        var dateMatch = DatePattern.Match(normalized);
        if (dateMatch.Success)
        {
            confidence += 0.15m;
            hints.Add($"date:{dateMatch.Value}");
        }

        // PATCH 30.04: No hard gate, but adjust confidence
        // Entity + Season/Date without trigger can still route at lower confidence
        if (!hasMetricTrigger && matchedEntities.Count > 0 && (seasonMatch.Success || dateMatch.Success))
        {
            // Implicit analytics: "models season 25/26?" -> boost confidence
            confidence += 0.15m;
            hints.Add("implicit_analytics");
        }

        if (hasMetricTrigger && matchedEntities.Count == 0 && (seasonMatch.Success || dateMatch.Success))
        {
            confidence += 0.05m;
            hints.Add("metric_with_time_context");
        }

        // Apply thresholds
        var isAnalytics = confidence >= RouteToAnalytics;
        var isBorderline = confidence >= SkipAnalytics && confidence < RouteToAnalytics;

        if (isBorderline)
            hints.Add("borderline");

        hints.Add("heuristic");

        return new AnalyticsIntentResult
        {
            IsAnalytics = isAnalytics,
            IsBorderline = isBorderline,
            Confidence = Math.Min(confidence, 1.0m),
            Hints = hints,
            DetectedLanguage = IsLikelyVietnamese(normalized) ? "vi" : "en",
            EntityHint = matchedEntities.FirstOrDefault()
        };
    }

    /// <summary>
    /// Check for obvious non-analytics patterns.
    /// </summary>
    private static bool IsObviousNonAnalytics(string normalized)
    {
        return NonAnalyticsPatternsNormalized.Any(pattern => ContainsWholePhrase(normalized, pattern));
    }

    private static string NormalizeForRules(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        var formD = lower.Normalize(NormalizationForm.FormD);
        var buffer = new char[formD.Length];
        var index = 0;

        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            buffer[index++] = c switch
            {
                '\u0111' => 'd',
                _ => c
            };
        }

        return new string(buffer, 0, index).Normalize(NormalizationForm.FormC);
    }

    private static bool ContainsWholePhrase(string normalized, string phrase)
    {
        var pattern = $@"(?<![\p{{L}}\p{{Nd}}]){Regex.Escape(phrase)}(?![\p{{L}}\p{{Nd}}])";
        return Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsLikelyVietnamese(string normalized)
    {
        var viSignals = new[]
        {
            "mua", "mau", "don", "khach", "hang", "ban chay", "bo suu tap"
        };

        var viOnlyTriggers = ViMetricTriggersNormalized.Where(trigger => trigger != "top");

        return viOnlyTriggers.Any(trigger => normalized.Contains(trigger))
            || viSignals.Any(signal => ContainsWholePhrase(normalized, signal));
    }
}

/// <summary>
/// Result of analytics intent detection.
/// PATCH 30.04: Added IsBorderline and EntityHint. Changed to record for with-expression.
/// </summary>
public sealed record AnalyticsIntentResult
{
    public bool IsAnalytics { get; set; }
    public bool IsBorderline { get; set; }
    public decimal Confidence { get; set; }
    public List<string> Hints { get; set; } = new();
    public string DetectedLanguage { get; set; } = "en";
    public string? EntityHint { get; set; }

    /// <summary>PATCH 31.07: Factory for neutralized intent (after RBAC denial).</summary>
    public static AnalyticsIntentResult None() => new()
    {
        IsAnalytics = false,
        Confidence = 0,
        Hints = new List<string>()
    };
}
