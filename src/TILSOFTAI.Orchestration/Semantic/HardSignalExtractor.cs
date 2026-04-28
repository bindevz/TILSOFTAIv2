using System.Text.RegularExpressions;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Semantic;

public sealed partial class HardSignalExtractor : IHardSignalExtractor
{
    private static readonly string[] BusinessTerms =
    [
        "inventory", "stock", "warehouse", "movement", "po", "purchase", "supplier",
        "sales", "order", "customer", "receivable", "ar", "debt", "overdue",
        "model", "mã hàng", "tồn kho", "nhập xuất", "công nợ", "khách hàng",
        "nhà cung cấp", "đơn hàng", "đơn mua", "mẫu"
    ];

    public HardSignalSet Extract(string message, string locale, TilsoftExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new HardSignalSet { DetectedLanguage = NormalizeLanguage(locale) };
        }

        var codes = CodeRegex()
            .Matches(message)
            .Select(match => new CodeSignal
            {
                Text = match.Value,
                PossibleType = InferCodeType(match.Value)
            })
            .DistinctBy(signal => signal.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var dates = DateRegex()
            .Matches(message)
            .Select(match => new DateSignal
            {
                Text = match.Value,
                Kind = InferDateKind(match.Value)
            })
            .DistinctBy(signal => signal.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var numbers = NumberRegex()
            .Matches(message)
            .Select(match => new NumberSignal
            {
                Text = match.Value,
                Value = decimal.TryParse(match.Groups["value"].Value, out var value) ? value : null,
                Unit = string.IsNullOrWhiteSpace(match.Groups["unit"].Value) ? null : match.Groups["unit"].Value
            })
            .ToArray();

        var normalized = SemanticScoring.Normalize(message);
        var keywords = BusinessTerms
            .Where(term => normalized.Contains(SemanticScoring.Normalize(term), StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new HardSignalSet
        {
            Codes = codes,
            Dates = dates,
            Numbers = numbers,
            BusinessKeywords = keywords,
            DetectedLanguage = DetectLanguage(message, locale)
        };
    }

    private static string? InferCodeType(string code)
    {
        if (code.StartsWith("PO", StringComparison.OrdinalIgnoreCase)) return "po";
        if (code.StartsWith("SO", StringComparison.OrdinalIgnoreCase)) return "so";
        if (code.StartsWith("INV", StringComparison.OrdinalIgnoreCase)) return "invoice";
        return code.Contains('-', StringComparison.Ordinal) ? "item_or_business_code" : null;
    }

    private static string? InferDateKind(string text)
    {
        var normalized = SemanticScoring.Normalize(text);
        if (normalized.Contains("last month", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("thang truoc", StringComparison.OrdinalIgnoreCase))
        {
            return "relative_month";
        }

        return "date";
    }

    private static string DetectLanguage(string message, string locale)
    {
        if (VietnameseRegex().IsMatch(message))
        {
            return "vi-VN";
        }

        return NormalizeLanguage(locale);
    }

    private static string NormalizeLanguage(string locale) =>
        string.IsNullOrWhiteSpace(locale) ? "vi-VN" : locale;

    [GeneratedRegex(@"\b(?:PO|SO|INV)-?\d+[A-Z0-9-]*\b|\b[A-Z]{2,}[A-Z0-9]+(?:-[A-Z0-9]+)+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"\b(?:last month|this month|last week|today|yesterday|tomorrow|tháng trước|thang truoc|hôm nay|hom nay|\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}/\d{2,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"\b(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>pcs|pc|cái|cai|usd|vnd|kg|m|m2)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex("[ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VietnameseRegex();
}
