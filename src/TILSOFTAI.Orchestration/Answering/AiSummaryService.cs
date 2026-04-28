namespace TILSOFTAI.Orchestration.Answering;

public sealed class AiSummaryService
{
    public Task<string> SummarizeAsync(
        AnswerComposerRequest request,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> safeRows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(safeRows);

        return Task.FromResult(BuildDeterministicSummary(request, safeRows));
    }

    private static string BuildDeterministicSummary(
        AnswerComposerRequest request,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> safeRows)
    {
        if (request.RowCount == 0)
        {
            return IsVietnamese(request.Locale)
                ? $"Khong tim thay du lieu cho {request.CapabilityKey} voi bo loc da cung cap."
                : $"No data was found for {request.CapabilityKey} with the provided filters.";
        }

        var displayed = Math.Min(safeRows.Count, request.RowCount);
        return IsVietnamese(request.Locale)
            ? $"Tim thay {request.RowCount} dong cho {request.CapabilityKey}; hien thi {displayed} dong dau tien."
            : $"Found {request.RowCount} rows for {request.CapabilityKey}; showing the first {displayed}.";
    }

    private static bool IsVietnamese(string locale) =>
        locale.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
}
