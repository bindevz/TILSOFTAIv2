using System.Text.Json;

namespace TILSOFTAI.Orchestration.Answering.Narration;

public sealed class AnswerNarrationPromptBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public string BuildSystemInstruction() =>
        """
        You are an ERP data summarizer.
        Use only supplied rows, schema, arguments, and metadata.
        Do not invent fields, totals, causes, recommendations, or missing records.
        Do not expose hidden or masked fields.
        Do not mention stored procedure names.
        If data is insufficient, say so.
        Return JSON only.
        """;

    public string BuildUserMessage(AnswerNarrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var catalogInstruction = ResolveCatalogInstruction(request.Locale, request.AnswerPolicy.Summary.InstructionsByLocale);
        var payload = new
        {
            request.Locale,
            request.CapabilityKey,
            request.UserQuestion,
            catalog = new
            {
                name = request.CapabilityName,
                description = request.CapabilityDescription
            },
            arguments = request.Arguments,
            resultSchema = request.ResultSchema,
            rows = request.Rows,
            request.RowCount,
            answerPolicy = request.AnswerPolicy,
            summaryPolicy = new
            {
                request.AnswerPolicy.Summary.Mode,
                request.AnswerPolicy.Summary.Style,
                request.AnswerPolicy.Summary.MaxSentences,
                request.AnswerPolicy.Summary.IncludeFilters,
                request.AnswerPolicy.Summary.IncludeRowCount,
                request.AnswerPolicy.Summary.IncludeCaveats,
                catalogInstruction,
                request.AnswerPolicy.Summary.ForbiddenClaims
            },
            sensitivityPolicy = request.SensitivityPolicy,
            executionMetadata = new
            {
                request.ExecutionMetadata.CorrelationId,
                request.ExecutionMetadata.Operation
            },
            outputSchema = new
            {
                text = "Short business summary based only on supplied data.",
                confidence = 0.85,
                usedColumns = new[] { "ColumnA", "ColumnB" },
                warnings = Array.Empty<string>()
            }
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    internal static string ResolveCatalogInstruction(
        string? locale,
        IReadOnlyDictionary<string, string> instructionsByLocale)
    {
        if (instructionsByLocale.Count == 0)
        {
            return "Summarize the supplied rows concisely. Use only supplied data.";
        }

        if (!string.IsNullOrWhiteSpace(locale)
            && instructionsByLocale.TryGetValue(locale, out var exact)
            && !string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        if (instructionsByLocale.TryGetValue("vi-VN", out var vi) && !string.IsNullOrWhiteSpace(vi))
        {
            return vi;
        }

        if (instructionsByLocale.TryGetValue("en-US", out var en) && !string.IsNullOrWhiteSpace(en))
        {
            return en;
        }

        return instructionsByLocale.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "Summarize the supplied rows concisely. Use only supplied data.";
    }
}
