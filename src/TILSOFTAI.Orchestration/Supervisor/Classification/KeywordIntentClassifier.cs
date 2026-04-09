using Microsoft.Extensions.Logging;

namespace TILSOFTAI.Supervisor.Classification;

/// <summary>
/// Sprint 2 keyword-based intent classifier.
/// Maps known domain keywords to domain hints without LLM overhead.
/// Falls back to unclassified when no strong keyword match is found.
/// </summary>
public sealed class KeywordIntentClassifier : IIntentClassifier
{
    private readonly ILogger<KeywordIntentClassifier> _logger;

    /// <summary>
    /// Domain keyword mappings: domain → keywords.
    /// Ordered by priority; first domain to match wins.
    /// </summary>
    private static readonly IReadOnlyList<DomainKeywordRule> Rules = new[]
    {
        new DomainKeywordRule("accounting", new[]
        {
            "invoice", "hóa đơn", "payment", "thanh toán", "receivable", "payable",
            "journal", "ledger", "sổ cái", "debit", "credit", "balance sheet",
            "bảng cân đối", "profit", "loss", "lãi", "lỗ", "revenue", "doanh thu",
            "cost", "chi phí", "tax", "thuế", "fiscal", "tài chính",
            "account", "tài khoản", "accounting", "kế toán"
        }),
        new DomainKeywordRule("warehouse", new[]
        {
            "warehouse", "kho", "inventory", "tồn kho", "stock", "shipment",
            "lô hàng", "container", "packaging", "đóng gói", "cbm", "pallet",
            "delivery", "giao hàng", "receipt", "nhập kho", "dispatch", "xuất kho",
            "storage", "lưu kho", "bin", "location", "vị trí"
        })
    };

    /// <summary>
    /// Sprint 3: write-intent keywords (mutation language).
    /// When detected, IntentType is set to "write" regardless of domain.
    /// </summary>
    private static readonly IReadOnlyList<string> WriteKeywords = new[]
    {
        "update", "delete", "insert", "modify", "remove", "add", "create", "change",
        "cập nhật", "xóa", "thêm", "sửa", "tạo mới", "thay đổi"
    };

    /// <summary>
    /// Minimum number of keyword hits to classify as a domain match.
    /// </summary>
    private const int MinHits = 1;

    /// <summary>
    /// Confidence threshold below which classification is considered too weak.
    /// </summary>
    private const decimal ConfidenceThreshold = 0.3m;

    public KeywordIntentClassifier(ILogger<KeywordIntentClassifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IntentClassification> ClassifyAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.FromResult(IntentClassification.Unclassified("Empty input"));
        }

        var normalizedInput = input.ToLowerInvariant();
        var bestDomain = (string?)null;
        var bestScore = 0;
        var bestKeywords = new List<string>();

        foreach (var rule in Rules)
        {
            var matchedKeywords = new List<string>();
            foreach (var keyword in rule.Keywords)
            {
                if (normalizedInput.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    matchedKeywords.Add(keyword);
                }
            }

            if (matchedKeywords.Count >= MinHits && matchedKeywords.Count > bestScore)
            {
                bestDomain = rule.Domain;
                bestScore = matchedKeywords.Count;
                bestKeywords = matchedKeywords;
            }
        }

        if (bestDomain is null || bestScore < MinHits)
        {
            _logger.LogDebug(
                "IntentClassification | Result: unclassified | Input: {InputPreview}",
                input.Length > 60 ? input[..60] + "..." : input);

            return Task.FromResult(IntentClassification.Unclassified("No domain keywords matched"));
        }

        var confidence = Math.Min(1.0m, (decimal)bestScore / 3.0m);

        if (confidence < ConfidenceThreshold)
        {
            _logger.LogDebug(
                "IntentClassification | Result: below_threshold | Domain: {Domain} | Confidence: {Confidence}",
                bestDomain, confidence);

            return Task.FromResult(IntentClassification.Unclassified(
                $"Domain '{bestDomain}' matched but confidence {confidence:F2} < threshold {ConfidenceThreshold:F2}"));
        }

        // Sprint 3: detect write intent
        var intentType = "query";
        var matchedWriteKeywords = new List<string>();
        foreach (var wk in WriteKeywords)
        {
            if (normalizedInput.Contains(wk, StringComparison.OrdinalIgnoreCase))
            {
                matchedWriteKeywords.Add(wk);
            }
        }

        if (matchedWriteKeywords.Count > 0)
        {
            intentType = "write";
        }

        var result = new IntentClassification
        {
            DomainHint = bestDomain,
            Confidence = confidence,
            IntentType = intentType,
            Reasons = matchedWriteKeywords.Count > 0
                ? new[] { $"Matched keywords: [{string.Join(", ", bestKeywords)}]", $"Write keywords: [{string.Join(", ", matchedWriteKeywords)}]" }
                : new[] { $"Matched keywords: [{string.Join(", ", bestKeywords)}]" }
        };

        _logger.LogInformation(
            "IntentClassification | Domain: {Domain} | IntentType: {IntentType} | Confidence: {Confidence} | Keywords: [{Keywords}]",
            bestDomain, intentType, confidence, string.Join(", ", bestKeywords));

        return Task.FromResult(result);
    }

    private sealed record DomainKeywordRule(string Domain, IReadOnlyList<string> Keywords);
}
