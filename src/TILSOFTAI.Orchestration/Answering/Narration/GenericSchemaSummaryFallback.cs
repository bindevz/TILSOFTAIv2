namespace TILSOFTAI.Orchestration.Answering.Narration;

public sealed class GenericSchemaSummaryFallback
{
    public AnswerNarrationResult Generate(AnswerNarrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var text = IsVietnamese(request.Locale)
            ? BuildVietnameseText(request)
            : BuildEnglishText(request);

        return new AnswerNarrationResult
        {
            Text = text,
            UsedColumns = ResolveVisibleColumnNames(request),
            UsedFallback = true
        };
    }

    private static string BuildEnglishText(AnswerNarrationRequest request)
    {
        var subject = ResolveSubject(request);
        var text = $"Found {request.RowCount} rows for \"{subject}\". Showing the available structured result.";
        if (IsTruncated(request))
        {
            text += $" Narration used the first {request.Rows.Count} rows.";
        }

        var labels = ResolveVisibleColumnLabels(request);
        if (labels.Count > 0)
        {
            text += $" Visible columns: {string.Join(", ", labels)}.";
        }

        return text;
    }

    private static string BuildVietnameseText(AnswerNarrationRequest request)
    {
        var subject = ResolveSubject(request);
        var text = $"Tìm thấy {request.RowCount} dòng cho \"{subject}\". Dữ liệu được hiển thị trong bảng bên dưới.";
        if (IsTruncated(request))
        {
            text += $" Phần tường thuật dùng {request.Rows.Count} dòng đầu tiên.";
        }

        var labels = ResolveVisibleColumnLabels(request);
        if (labels.Count > 0)
        {
            text += $" Cột hiển thị: {string.Join(", ", labels)}.";
        }

        return text;
    }

    private static string ResolveSubject(AnswerNarrationRequest request) =>
        FirstNonBlank(request.CapabilityName, request.CapabilityDescription, request.CapabilityKey);

    private static IReadOnlyList<string> ResolveVisibleColumnLabels(AnswerNarrationRequest request) =>
        request.ResultSchema?.Columns
            .Where(column => column.Visible)
            .Select(column => IsVietnamese(request.Locale)
                ? FirstNonBlank(column.LabelVi, column.Label, column.Name)
                : FirstNonBlank(column.Label, column.Name))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToArray()
        ?? Array.Empty<string>();

    private static IReadOnlyList<string> ResolveVisibleColumnNames(AnswerNarrationRequest request) =>
        request.ResultSchema?.Columns
            .Where(column => column.Visible)
            .Select(column => column.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray()
        ?? request.Rows.FirstOrDefault()?.Keys.ToArray()
        ?? Array.Empty<string>();

    private static string FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "results";

    private static bool IsTruncated(AnswerNarrationRequest request) =>
        request.RowCount > request.Rows.Count;

    private static bool IsVietnamese(string locale) =>
        locale.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
}
