using System.Globalization;
using TILSOFTAI.Orchestration.Execution;

namespace TILSOFTAI.Orchestration.Answering;

public sealed class StructuredAnswerComposer : IAnswerComposer
{
    private readonly RawJsonAnswerComposer _rawJsonComposer;
    private readonly AiSummaryService _summaryService;

    public StructuredAnswerComposer(
        RawJsonAnswerComposer rawJsonComposer,
        AiSummaryService summaryService)
    {
        _rawJsonComposer = rawJsonComposer ?? throw new ArgumentNullException(nameof(rawJsonComposer));
        _summaryService = summaryService ?? throw new ArgumentNullException(nameof(summaryService));
    }

    public Task<AssistantAnswer> ComposeAsync(
        AnswerComposerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Mode == AnswerMode.RawJson)
        {
            return _rawJsonComposer.ComposeAsync(request, cancellationToken);
        }

        return ComposeStructuredAsync(request, cancellationToken);
    }

    private async Task<AssistantAnswer> ComposeStructuredAsync(
        AnswerComposerRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ClarificationQuestion))
        {
            return FollowUp(request, request.ClarificationQuestion, request.MissingArguments);
        }

        if (string.Equals(request.ErrorCode, "ARGUMENT_VALIDATION_FAILED", StringComparison.OrdinalIgnoreCase))
        {
            var question = BuildValidationQuestion(request);
            return FollowUp(request, question, request.MissingArguments.Concat(request.InvalidArguments).ToArray());
        }

        if (!string.IsNullOrWhiteSpace(request.ErrorCode))
        {
            var text = string.IsNullOrWhiteSpace(request.ErrorMessage)
                ? request.ErrorCode
                : request.ErrorMessage;
            return TextOnly(request, "error", text);
        }

        if (IsWritePreview(request))
        {
            return Confirmation(request);
        }

        if (request.Result is CompositeResultBundle bundle)
        {
            return Composite(request, bundle);
        }

        var safeRows = AnswerDataSanitizer.ApplySensitivity(request.Rows, request.SensitivityPolicy);
        if (request.RowCount == 0)
        {
            return TextOnly(
                request,
                "no_data",
                FormatNoDataText(request));
        }

        var maxRows = Math.Max(1, request.AnswerPolicy.MaxRowsForChat);
        var visibleRows = safeRows.Take(maxRows).ToArray();
        var truncated = request.RowCount > visibleRows.Length;
        var summary = await _summaryService.SummarizeAsync(request, visibleRows, cancellationToken)
            .ConfigureAwait(false);

        var blocks = new List<AnswerBlock>
        {
            new SummaryBlock(summary),
            BuildTableBlock(request, visibleRows, truncated)
        };

        var chart = TryBuildChartBlock(request, visibleRows);
        if (chart is not null)
        {
            blocks.Add(chart);
        }

        var followUps = truncated
            ? new[] { IsVietnamese(request.Locale) ? "Thu hẹp bộ lọc hoặc xuất kết quả đầy đủ." : "Narrow the filters or export the full result set." }
            : Array.Empty<string>();

        return new AssistantAnswer
        {
            AnswerType = "structured",
            Text = summary,
            Blocks = blocks,
            FollowUpQuestions = followUps,
            Provenance = CreateProvenance(request),
            SelectedAgentId = "microsoft-agent-router",
            Detail = CreateStructuredDetail(request, "structured", blocks, followUps, truncated)
        };
    }

    private static AssistantAnswer FollowUp(
        AnswerComposerRequest request,
        string question,
        IReadOnlyList<string> options) => new()
        {
            AnswerType = "follow_up",
            Text = question,
            Blocks = [new FollowUpBlock(question, options)],
            FollowUpQuestions = [question],
            Provenance = CreateProvenance(request),
            SelectedAgentId = "microsoft-agent-router",
            Detail = CreateStructuredDetail(request, "follow_up", [new FollowUpBlock(question, options)], [question], text: question)
        };

    private static AssistantAnswer TextOnly(
        AnswerComposerRequest request,
        string answerType,
        string text) => new()
        {
            AnswerType = answerType,
            Text = text,
            Blocks = [new TextBlock(text)],
            Provenance = CreateProvenance(request),
            SelectedAgentId = "microsoft-agent-router",
            Detail = CreateStructuredDetail(request, answerType, [new TextBlock(text)], text: text)
        };

    private static AssistantAnswer Confirmation(AnswerComposerRequest request)
    {
        var title = IsVietnamese(request.Locale) ? "Xác nhận thao tác" : "Confirm action";
        var summary = IsVietnamese(request.Locale)
            ? $"Kiểm tra trước thao tác {request.CapabilityKey}."
            : $"Review the proposed {request.CapabilityKey} action.";
        var draftAction = AnswerDataSanitizer.ApplySensitivity(
            request.DraftAction ?? new Dictionary<string, object?>(request.Arguments, StringComparer.OrdinalIgnoreCase),
            request.SensitivityPolicy);

        return new AssistantAnswer
        {
            AnswerType = "confirmation",
            Text = summary,
            Blocks =
            [
                new ConfirmationBlock(
                    title,
                    summary,
                    draftAction)
            ],
            Provenance = CreateProvenance(request),
            SelectedAgentId = "microsoft-agent-router",
            Detail = CreateStructuredDetail(
                request,
                "confirmation",
                [new ConfirmationBlock(title, summary, draftAction)],
                draftAction: draftAction,
                text: summary)
        };
    }

    private static AssistantAnswer Composite(AnswerComposerRequest request, CompositeResultBundle bundle)
    {
        var text = IsVietnamese(request.Locale)
            ? $"Tổng hợp {bundle.Sections.Count} phần cho {request.CapabilityKey}."
            : $"Compiled {bundle.Sections.Count} sections for {request.CapabilityKey}.";

        var blocks = new List<AnswerBlock> { new SummaryBlock(text) };
        foreach (var section in bundle.Sections)
        {
            var sectionText = section.Success
                ? $"{section.CapabilityKey}: {section.RowCount} rows."
                : $"{section.CapabilityKey}: {section.ErrorCode ?? "failed"}.";
            blocks.Add(new TextBlock(sectionText));

            if (section.Rows.Count > 0)
            {
                var maxRows = Math.Max(1, request.AnswerPolicy.MaxRowsForChat);
                var safeRows = AnswerDataSanitizer.ApplySensitivity(section.Rows, request.SensitivityPolicy);
                var sectionRequest = request with
                {
                    Rows = safeRows,
                    RowCount = section.RowCount,
                    ResultSchema = null
                };
                blocks.Add(BuildTableBlock(
                    sectionRequest,
                    safeRows.Take(maxRows).ToArray(),
                    section.RowCount > maxRows));
            }
        }

        return new AssistantAnswer
        {
            AnswerType = "composite",
            Text = text,
            Blocks = blocks,
            Provenance = CreateProvenance(request),
            SelectedAgentId = "microsoft-agent-router",
            Detail = CreateStructuredDetail(
                request,
                "composite",
                blocks,
                result: AnswerDataSanitizer.ApplySensitivity(bundle, request.SensitivityPolicy),
                text: text)
        };
    }

    private static TableBlock BuildTableBlock(
        AnswerComposerRequest request,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        bool truncated)
    {
        var columns = ResolveVisibleColumns(request, rows);
        var tableRows = rows
            .Select(row => (IReadOnlyList<object?>)columns
                .Select(column => FormatValue(row.TryGetValue(column.Name, out var value) ? value : null, request.Locale))
                .ToArray())
            .ToArray();

        return new TableBlock(columns.Select(column => column.Label).ToArray(), tableRows, request.RowCount, truncated);
    }

    private static ChartBlock? TryBuildChartBlock(
        AnswerComposerRequest request,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        var hint = request.ResultSchema?.ChartHints.FirstOrDefault();
        if (hint is not null)
        {
            return new ChartBlock(hint.Type, hint.Category, hint.Value, rows);
        }

        var columns = request.ResultSchema?.Columns ?? Array.Empty<ResultColumn>();
        var category = columns.FirstOrDefault(column => IsCategory(column) || IsDateTime(column));
        var measure = columns.FirstOrDefault(IsMeasure);
        if (category is null || measure is null)
        {
            return null;
        }

        var chartType = IsDateTime(category) ? "line" : "bar";
        return new ChartBlock(chartType, category.Name, measure.Name, rows);
    }

    private static IReadOnlyList<TableColumnSpec> ResolveVisibleColumns(
        AnswerComposerRequest request,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var hidden = request.SensitivityPolicy.HiddenColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var schemaColumns = request.ResultSchema?.Columns
            .Where(column => column.Visible && !hidden.Contains(column.Name))
            .Select(column => new TableColumnSpec(column.Name, string.IsNullOrWhiteSpace(column.Label) ? column.Name : column.Label!))
            .ToArray();

        if (schemaColumns is { Length: > 0 })
        {
            return schemaColumns;
        }

        return rows.FirstOrDefault()?.Keys
            .Where(column => !hidden.Contains(column))
            .Select(column => new TableColumnSpec(column, column))
            .ToArray()
            ?? Array.Empty<TableColumnSpec>();
    }

    private static string BuildValidationQuestion(AnswerComposerRequest request)
    {
        if (request.MissingArguments.Count > 0)
        {
            return IsVietnamese(request.Locale)
                ? $"Vui lòng cung cấp: {string.Join(", ", request.MissingArguments.Select(FormatArgumentName))}."
                : $"Please provide: {string.Join(", ", request.MissingArguments.Select(FormatArgumentName))}.";
        }

        return IsVietnamese(request.Locale)
            ? "Vui lòng kiểm tra lại tham số."
            : "Please correct the capability arguments.";
    }

    private static string FormatNoDataText(AnswerComposerRequest request)
    {
        var filters = request.Arguments.Count == 0
            ? string.Empty
            : $" ({string.Join(", ", AnswerDataSanitizer
                .ApplySensitivity(request.Arguments, request.SensitivityPolicy)
                .Select(pair => $"{pair.Key}={FormatValue(pair.Value, request.Locale)}"))})";

        return IsVietnamese(request.Locale)
            ? $"Không tìm thấy dữ liệu cho {request.CapabilityKey}{filters}."
            : $"No data was found for {request.CapabilityKey}{filters}.";
    }

    private static object? FormatValue(object? value, string locale)
    {
        if (value is null)
        {
            return null;
        }

        var culture = ResolveCulture(locale);
        return value switch
        {
            DateTime dateTime => dateTime.ToString("d", culture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("d", culture),
            decimal decimalValue => decimalValue.ToString("N2", culture),
            double doubleValue => doubleValue.ToString("N2", culture),
            float floatValue => floatValue.ToString("N2", culture),
            _ => value
        };
    }

    private static CultureInfo ResolveCulture(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(locale) ? "vi-VN" : locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("vi-VN");
        }
    }

    private static bool IsWritePreview(AnswerComposerRequest request) =>
        request.ExecutionMetadata.Operation?.Equals("write_preview", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsCategory(ResultColumn column) =>
        column.Role?.Equals("dimension", StringComparison.OrdinalIgnoreCase) == true
        || column.Type?.Equals("string", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsMeasure(ResultColumn column) =>
        column.Role?.Equals("measure", StringComparison.OrdinalIgnoreCase) == true
        || column.Type is not null && (column.Type.Equals("decimal", StringComparison.OrdinalIgnoreCase)
            || column.Type.Equals("number", StringComparison.OrdinalIgnoreCase)
            || column.Type.Equals("integer", StringComparison.OrdinalIgnoreCase));

    private static bool IsDateTime(ResultColumn column) =>
        column.Type is not null && (column.Type.Equals("date", StringComparison.OrdinalIgnoreCase)
            || column.Type.Equals("datetime", StringComparison.OrdinalIgnoreCase)
            || column.Type.Equals("time", StringComparison.OrdinalIgnoreCase));

    private static bool IsVietnamese(string locale) =>
        locale.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

    private static string FormatArgumentName(string argumentName)
    {
        var trimmed = argumentName.TrimStart('@');
        return string.Equals(trimmed, "model_code", StringComparison.OrdinalIgnoreCase)
            ? "modelCode"
            : trimmed;
    }

    private static AnswerProvenance CreateProvenance(AnswerComposerRequest request) => new()
    {
        CapabilityKey = request.CapabilityKey,
        RowCount = request.RowCount,
        CorrelationId = request.ExecutionMetadata.CorrelationId,
        ProcedureName = request.ProcedureName
    };

    private static object CreateStructuredDetail(
        AnswerComposerRequest request,
        string answerType,
        IReadOnlyList<AnswerBlock> blocks,
        IReadOnlyList<string>? followUpQuestions = null,
        bool truncated = false,
        object? result = null,
        IReadOnlyDictionary<string, object?>? draftAction = null,
        string? text = null)
    {
        var provenance = CreateProvenance(request);
        return new
        {
            mode = "structured",
            answerType,
            text = text
                ?? blocks.OfType<SummaryBlock>().FirstOrDefault()?.Content
                ?? blocks.OfType<TextBlock>().FirstOrDefault()?.Content
                ?? string.Empty,
            blocks,
            followUpQuestions = followUpQuestions ?? Array.Empty<string>(),
            provenance,
            capabilityKey = request.CapabilityKey,
            procedureName = request.ProcedureName,
            arguments = AnswerDataSanitizer.ApplySensitivity(request.Arguments, request.SensitivityPolicy),
            rowCount = request.RowCount,
            errorCode = request.ErrorCode,
            missingArguments = request.MissingArguments.Select(FormatArgumentName).ToArray(),
            invalidArguments = request.InvalidArguments.Select(FormatArgumentName).ToArray(),
            truncated,
            result,
            draftAction
        };
    }

    private sealed record TableColumnSpec(string Name, string Label);
}

internal static class AnswerDataSanitizer
{
    public static IReadOnlyDictionary<string, object?> ApplySensitivity(
        IReadOnlyDictionary<string, object?> values,
        SensitivityPolicy policy)
    {
        var hidden = policy.HiddenColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var masked = policy.MaskColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var safe = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in values)
        {
            if (hidden.Contains(key))
            {
                continue;
            }

            safe[key] = masked.Contains(key) ? policy.MaskValue : ApplySensitivity(value, policy);
        }

        return safe;
    }

    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> ApplySensitivity(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        SensitivityPolicy policy)
    {
        var hidden = policy.HiddenColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var masked = policy.MaskColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows
            .Select(row =>
            {
                var safe = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in row)
                {
                    if (hidden.Contains(key))
                    {
                        continue;
                    }

                    safe[key] = masked.Contains(key) ? policy.MaskValue : ApplySensitivity(value, policy);
                }

                return (IReadOnlyDictionary<string, object?>)safe;
            })
            .ToArray();
    }

    public static CompositeResultBundle ApplySensitivity(
        CompositeResultBundle bundle,
        SensitivityPolicy policy) =>
        bundle with
        {
            Sections = bundle.Sections
                .Select(section => section with
                {
                    Rows = ApplySensitivity(section.Rows, policy)
                })
                .ToArray()
        };

    public static object? ApplySensitivity(object? value, SensitivityPolicy policy)
    {
        if (value is null || value is string)
        {
            return value;
        }

        if (value is CompositeResultBundle bundle)
        {
            return ApplySensitivity(bundle, policy);
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return ApplySensitivity(dictionary, policy);
        }

        if (value is IEnumerable<IReadOnlyDictionary<string, object?>> rows)
        {
            return ApplySensitivity(rows.ToArray(), policy);
        }

        if (value is System.Collections.IDictionary nonGenericDictionary)
        {
            var safeDictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in nonGenericDictionary)
            {
                if (entry.Key is string key)
                {
                    safeDictionary[key] = entry.Value;
                }
            }

            return ApplySensitivity(safeDictionary, policy);
        }

        return value;
    }
}
