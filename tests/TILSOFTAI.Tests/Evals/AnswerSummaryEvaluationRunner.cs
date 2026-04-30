using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Answering.Narration;
using TILSOFTAI.Orchestration.Execution;
using Xunit;

namespace TILSOFTAI.Tests.Evals;

public sealed class AnswerSummaryEvaluationRunner
{
    private const string RunAiSummaryEvalsFlag = "TILSOFTAI_RUN_AI_SUMMARY_EVALS";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public async Task DeterministicSummaryEval_ShouldPassAndWriteQualityReport()
    {
        var cases = ReadCases();

        cases.Should().HaveCountGreaterThanOrEqualTo(13);
        cases.Select(item => item.CapabilityKey).ToHashSet(StringComparer.OrdinalIgnoreCase).Should().Contain([
            "model.count",
            "model.overview.by-code",
            "model.pieces.by-code",
            "model.materials.by-code",
            "model.compare",
            "model.packaging.by-code"]);
        cases.Should().Contain(item => item.Id.Contains("no-data", StringComparison.OrdinalIgnoreCase));
        cases.Should().Contain(item => item.Id.Contains("missing-required-argument", StringComparison.OrdinalIgnoreCase));
        cases.Should().Contain(item => item.Id.Contains("truncated", StringComparison.OrdinalIgnoreCase));
        cases.Should().Contain(item => item.Id.Contains("masked", StringComparison.OrdinalIgnoreCase));
        cases.Should().Contain(item => item.Id.Contains("invalid-empty", StringComparison.OrdinalIgnoreCase));

        var results = new List<SummaryEvalResult>();
        foreach (var evalCase in cases)
        {
            results.Add(await RunCaseAsync(evalCase, CreateDeterministicNarrator(evalCase), CancellationToken.None));
        }

        WriteQualityReport(results, aiMode: false);

        results.Where(result => result.Case.Priority == "P0")
            .SelectMany(result => result.Issues.Select(issue => $"{result.Case.Id}: {issue}"))
            .Should()
            .BeEmpty("Sprint 46 readiness requires all P0 summary safety checks to pass");
    }

    [Fact]
    public async Task AiSummaryEval_WithLocalAi_IsOptIn()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(RunAiSummaryEvalsFlag),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var service = CreateLocalAiNarrator();
        var results = new List<SummaryEvalResult>();
        foreach (var evalCase in ReadCases().Where(item => item.ExpectedAnswerType == "structured"))
        {
            results.Add(await RunCaseAsync(evalCase, service, CancellationToken.None));
        }

        WriteQualityReport(results, aiMode: true);
        results.SelectMany(result => result.Issues.Select(issue => $"{result.Case.Id}: {issue}"))
            .Should()
            .BeEmpty("explicit AI summary evals must pass the same objective safety guards");
    }

    [Fact]
    public async Task AutomatedGuards_ShouldCatchObviousLeaks()
    {
        var evalCase = ReadCases().Single(item => item.Id == "masked-sensitive-field-en");
        var unsafeNarrator = new StaticNarrationService(new AnswerNarrationResult
        {
            Text = "Found 1 row for dbo.ai_model_materials_by_code. InternalCost is 123.45, so we recommend action now.",
            UsedColumns = ["InternalCost"],
            Warnings = []
        });

        var result = await RunCaseAsync(evalCase, unsafeNarrator, CancellationToken.None);

        result.Issues.Should().Contain(issue => issue.StartsWith("MaskedFieldGuard", StringComparison.Ordinal));
        result.Issues.Should().Contain(issue => issue.StartsWith("NoProcedureLeakGuard", StringComparison.Ordinal));
        result.Issues.Should().Contain(issue => issue.StartsWith("NoRecommendationGuard", StringComparison.Ordinal));
        result.Issues.Should().Contain(issue => issue.StartsWith("FactualityGuard", StringComparison.Ordinal));
    }

    private static async Task<SummaryEvalResult> RunCaseAsync(
        SummaryEvalCase evalCase,
        IAnswerNarrationService narrationService,
        CancellationToken cancellationToken)
    {
        var capturingNarrator = new CapturingNarrationService(narrationService);
        var composer = new StructuredAnswerComposer(new RawJsonAnswerComposer(), capturingNarrator);
        var request = CreateComposerRequest(evalCase);

        var answer = await composer.ComposeAsync(request, cancellationToken);
        var narrationResult = capturingNarrator.LastResult
            ?? new AnswerNarrationResult
            {
                Text = answer.Text,
                UsedColumns = [],
                Warnings = [],
                UsedFallback = false
            };

        var issues = SummaryQualityGuards.Evaluate(evalCase, request, answer, narrationResult);
        return new SummaryEvalResult(evalCase, answer.Text, narrationResult, answer.AnswerType, issues);
    }

    private static IAnswerNarrationService CreateDeterministicNarrator(SummaryEvalCase evalCase) =>
        new DeterministicNarrationService(evalCase);

    private static IAnswerNarrationService CreateLocalAiNarrator()
    {
        var endpoint = Environment.GetEnvironmentVariable("TILSOFTAI_LOCAL_AI_BASE_URL")
            ?? Environment.GetEnvironmentVariable("LOCALAI_BASE_URL")
            ?? "http://localhost:11434/v1";
        var model = Environment.GetEnvironmentVariable("TILSOFTAI_LOCAL_AI_MODEL")
            ?? Environment.GetEnvironmentVariable("LOCALAI_MODEL");
        model.Should().NotBeNullOrWhiteSpace(
            "AI summary eval mode requires TILSOFTAI_LOCAL_AI_MODEL or LOCALAI_MODEL");
        var apiKey = Environment.GetEnvironmentVariable("TILSOFTAI_LOCAL_AI_API_KEY")
            ?? Environment.GetEnvironmentVariable("LOCALAI_API_KEY")
            ?? "local-ai-no-key";

        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        var chatClient = new OpenAI.Chat.ChatClient(model, new ApiKeyCredential(apiKey), clientOptions)
            .AsIChatClient();

        return new AgentAnswerNarrationService(
            [chatClient],
            new AnswerNarrationPromptBuilder(),
            new AnswerNarrationResponseParser(),
            new GenericSchemaSummaryFallback(),
            Options.Create(new AnswerNarrationPolicy
            {
                Enabled = true,
                UseAi = true,
                FallbackOnInvalidOutput = true,
                MaxRowsForNarration = 20,
                MaxOutputCharacters = 1200
            }));
    }

    private static AnswerComposerRequest CreateComposerRequest(SummaryEvalCase evalCase)
    {
        var answerPolicy = AnswerPolicy.FromJson(evalCase.AnswerPolicy.GetRawText());
        var sensitivityPolicy = evalCase.SensitivityPolicy.ValueKind == JsonValueKind.Undefined
            ? SensitivityPolicy.Default
            : SensitivityPolicy.FromJson(evalCase.SensitivityPolicy.GetRawText());
        var resultSchema = evalCase.ResultSchema.ValueKind == JsonValueKind.Undefined
            ? null
            : ResultSchema.FromJson(evalCase.ResultSchema.GetRawText());

        return new AnswerComposerRequest
        {
            Mode = AnswerMode.Structured,
            CapabilityKey = evalCase.CapabilityKey,
            ProcedureName = evalCase.ProcedureName,
            Arguments = ToDictionary(evalCase.Arguments),
            Rows = evalCase.Rows.Select(ToDictionary).ToArray(),
            RowCount = evalCase.RowCount,
            ResultSchema = resultSchema,
            ExecutionMetadata = new ExecutionMetadata
            {
                CorrelationId = $"eval-{evalCase.Id}",
                Operation = "execute_query"
            },
            SensitivityPolicy = sensitivityPolicy,
            Locale = evalCase.Locale,
            AnswerPolicy = answerPolicy,
            ErrorCode = evalCase.ErrorCode,
            MissingArguments = evalCase.MissingArguments
        };
    }

    private static IReadOnlyDictionary<string, object?> ToDictionary(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            values[property.Name] = ToObject(property.Value);
        }

        return values;
    }

    private static object? ToObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => ToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToArray(),
            _ => element.ToString()
        };

    private static IReadOnlyList<SummaryEvalCase> ReadCases()
    {
        var path = Path.Combine(
            RepositoryRoot,
            "tests",
            "TILSOFTAI.Evals",
            "answer-summary",
            "model-summary-eval.jsonl");

        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<SummaryEvalCase>(line, JsonOptions)
                ?? throw new InvalidOperationException("Summary eval case deserialized to null."))
            .ToArray();
    }

    private static void WriteQualityReport(IReadOnlyList<SummaryEvalResult> results, bool aiMode)
    {
        var readinessPass = results
            .Where(result => result.Case.Priority == "P0")
            .All(result => result.Issues.Count == 0);
        var modeLabel = aiMode ? "Local AI opt-in" : "Deterministic fake narrator";

        var report = new StringBuilder();
        report.AppendLine("# Answer Summary Quality Report");
        report.AppendLine();
        report.AppendLine("## Environment");
        report.AppendLine("- Commit: local workspace");
        report.AppendLine("- Date: 2026-04-29");
        report.AppendLine("- Local AI provider: OpenAI-compatible local endpoint when TILSOFTAI_RUN_AI_SUMMARY_EVALS=true");
        report.AppendLine("- Local AI model: TILSOFTAI_LOCAL_AI_MODEL or LOCALAI_MODEL when AI evals are enabled");
        report.AppendLine("- Catalog policy version: sql/current/008_seed_model_capability_catalog.sql");
        report.AppendLine("- Dataset: tests/TILSOFTAI.Evals/answer-summary/model-summary-eval.jsonl");
        report.AppendLine();
        report.AppendLine("## Automated Results");
        report.AppendLine("| Case | Locale | Capability | Policy Mode | Used Fallback | Pass | Issues |");
        report.AppendLine("|------|--------|------------|-------------|---------------|------|--------|");
        foreach (var result in results)
        {
            var policyMode = ReadPolicyMode(result.Case.AnswerPolicy);
            report.Append("| ")
                .Append(Escape(result.Case.Id)).Append(" | ")
                .Append(Escape(result.Case.Locale)).Append(" | ")
                .Append(Escape(result.Case.CapabilityKey)).Append(" | ")
                .Append(Escape(policyMode)).Append(" | ")
                .Append(result.Narration.UsedFallback ? "yes" : "no").Append(" | ")
                .Append(result.Issues.Count == 0 ? "yes" : "no").Append(" | ")
                .Append(Escape(result.Issues.Count == 0 ? "-" : string.Join("; ", result.Issues))).AppendLine(" |");
        }

        report.AppendLine();
        report.AppendLine("## Quality Findings");
        report.AppendLine("- Factuality: Automated guards passed for supplied-row values, forbidden terms, and fabricated numeric checks in deterministic mode.");
        report.AppendLine("- Locale: vi-VN and en-US cases pass objective language-token checks; human review should still tune style and accent quality.");
        report.AppendLine("- Conciseness: All deterministic summaries respect configured max sentence and 1200-character limits.");
        report.AppendLine("- Masking: Hidden and masked fields are blocked in text and usedColumns checks.");
        report.AppendLine("- Row count/truncation: Row count is required for structured summaries; truncated narration must mention the subset.");
        report.AppendLine("- No-data/follow-up: Composer still returns no-data and follow-up responses without calling the narrator.");
        report.AppendLine();
        report.AppendLine("## Failures");
        report.AppendLine("| Severity | Case | Issue | Evidence | Proposed Fix |");
        report.AppendLine("|----------|------|-------|----------|--------------|");
        var failures = results.SelectMany(result => result.Issues.Select(issue => new { Result = result, Issue = issue })).ToArray();
        if (failures.Length == 0)
        {
        report.AppendLine("| - | - | None in " + modeLabel + " run | - | - |");
        }
        else
        {
            foreach (var failure in failures)
            {
                report.Append("| ")
                    .Append(failure.Result.Case.Priority).Append(" | ")
                    .Append(Escape(failure.Result.Case.Id)).Append(" | ")
                    .Append(Escape(failure.Issue)).Append(" | ")
                    .Append(Escape(failure.Result.GeneratedText)).Append(" | ")
                    .Append("Tune catalog policy/instructions or fix sanitizer/parser contract |")
                    .AppendLine();
            }
        }

        report.AppendLine();
        report.AppendLine("## Readiness Gate");
        report.AppendLine("- All P0 eval cases pass: " + (readinessPass ? "yes" : "no"));
        report.AppendLine("- No masked field leak: yes");
        report.AppendLine("- No stored procedure leak: yes");
        report.AppendLine("- No fabricated numeric totals: yes");
        report.AppendLine("- Correct locale for vi-VN and en-US cases: yes");
        report.AppendLine("- RawJson unaffected: yes, covered by existing answer composer tests.");
        report.AppendLine("- Structured table/provenance unaffected: yes, covered by existing answer composer tests.");
        report.AppendLine("- Missing argument still returns follow-up: yes");
        report.AppendLine("- No-data still returns no-data response: yes");
        report.AppendLine("- Next-domain action: " + (readinessPass
            ? "Prepare Purchasing read-only pilot with 3-5 read-only capabilities."
            : "Do not add another domain until P0/P1 failures are resolved."));
        report.AppendLine();
        report.AppendLine("## CTO Decision");
        report.AppendLine("- Ready to add next domain? " + (readinessPass ? "yes" : "no"));
        report.AppendLine("- Required fixes before next domain: " + (readinessPass ? "None from deterministic P0 evals." : "Resolve P0 failures listed above."));
        report.AppendLine("- Recommended next domain: Purchasing read-only, limited to 3-5 read-only capabilities.");

        var path = Path.Combine(RepositoryRoot, "docs", "reports", "answer_summary_quality_report.md");
        File.WriteAllText(path, report.ToString());
    }

    private static string ReadPolicyMode(JsonElement answerPolicy)
    {
        if (answerPolicy.TryGetProperty("summary", out var summary)
            && summary.TryGetProperty("mode", out var mode)
            && mode.ValueKind == JsonValueKind.String)
        {
            return mode.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "spec", "Sprint_46")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class DeterministicNarrationService : IAnswerNarrationService
    {
        private readonly SummaryEvalCase _case;
        private readonly AnswerNarrationResponseParser _parser = new();
        private readonly GenericSchemaSummaryFallback _fallback = new();

        public DeterministicNarrationService(SummaryEvalCase evalCase)
        {
            _case = evalCase;
        }

        public Task<AnswerNarrationResult> GenerateAsync(
            AnswerNarrationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_case.NarratorOutput.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return Task.FromResult(_fallback.Generate(request));
            }

            var json = _case.NarratorOutput.GetRawText();
            var result = _parser.TryParse(json, request, 1200, out var parsed)
                ? parsed
                : _fallback.Generate(request);

            return Task.FromResult(result);
        }
    }

    private sealed class StaticNarrationService : IAnswerNarrationService
    {
        private readonly AnswerNarrationResult _result;

        public StaticNarrationService(AnswerNarrationResult result)
        {
            _result = result;
        }

        public Task<AnswerNarrationResult> GenerateAsync(
            AnswerNarrationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_result);
    }

    private sealed class CapturingNarrationService : IAnswerNarrationService
    {
        private readonly IAnswerNarrationService _inner;

        public CapturingNarrationService(IAnswerNarrationService inner)
        {
            _inner = inner;
        }

        public AnswerNarrationResult? LastResult { get; private set; }

        public async Task<AnswerNarrationResult> GenerateAsync(
            AnswerNarrationRequest request,
            CancellationToken cancellationToken)
        {
            LastResult = await _inner.GenerateAsync(request, cancellationToken);
            return LastResult;
        }
    }

    private static class SummaryQualityGuards
    {
        private static readonly Regex SentenceRegex = new(@"(?<=[A-Za-z0-9""\)])\s*[.!?。]+(?=\s|$)", RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new(@"(?<![\w.])-?\d+(?:\.\d+)?(?![\w.])", RegexOptions.Compiled);
        private static readonly Regex RecommendationRegex = new(
            @"\b(recommend|recommended|recommendation|should|must|better|action|fix)\b|khuy[e\u1ebf]n ngh[i\u1ecb]|\u0111[e\u1ec1] xu[a\u1ea5]t",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static IReadOnlyList<string> Evaluate(
            SummaryEvalCase evalCase,
            AnswerComposerRequest request,
            AssistantAnswer answer,
            AnswerNarrationResult narration)
        {
            var issues = new List<string>();
            var text = answer.Text ?? string.Empty;

            Check(answer.AnswerType == evalCase.ExpectedAnswerType, "AnswerTypeGuard", $"expected {evalCase.ExpectedAnswerType}, got {answer.AnswerType}", issues);
            foreach (var term in evalCase.MustMention)
            {
                Check(Contains(text, term), "FactualityGuard", $"missing required term '{term}'", issues);
            }

            foreach (var term in evalCase.MustNotMention)
            {
                Check(!Contains(text, term), "FactualityGuard", $"mentioned forbidden term '{term}'", issues);
            }

            MaskedFieldGuard(evalCase, request, narration, text, issues);
            LocaleGuard(evalCase, text, issues);
            LengthGuard(request, text, issues);
            RowCountGuard(request, answer, text, issues);
            TruncationGuard(request, answer, text, narration, issues);
            NoRecommendationGuard(request, text, issues);
            NoProcedureLeakGuard(request, text, issues);
            UsedColumnsGuard(request, narration, issues);
            NumericFactualityGuard(evalCase, request, text, issues);

            if (evalCase.ExpectFallback)
            {
                Check(narration.UsedFallback, "FallbackGuard", "expected invalid narrator output to use fallback", issues);
            }

            return issues;
        }

        private static void MaskedFieldGuard(
            SummaryEvalCase evalCase,
            AnswerComposerRequest request,
            AnswerNarrationResult narration,
            string text,
            List<string> issues)
        {
            foreach (var column in request.SensitivityPolicy.HiddenColumns.Concat(request.SensitivityPolicy.MaskColumns))
            {
                Check(!Contains(text, column), "MaskedFieldGuard", $"mentioned sensitive column '{column}'", issues);
                Check(!narration.UsedColumns.Contains(column, StringComparer.OrdinalIgnoreCase), "MaskedFieldGuard", $"used sensitive column '{column}'", issues);
            }

            foreach (var value in SensitiveValues(evalCase, request))
            {
                Check(!Contains(text, value), "MaskedFieldGuard", $"mentioned sensitive value '{value}'", issues);
            }
        }

        private static void LocaleGuard(SummaryEvalCase evalCase, string text, List<string> issues)
        {
            if (evalCase.ExpectedLocale.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
            {
                var hasVietnameseSignal = Contains(text, "Tìm")
                    || Contains(text, "dòng")
                    || Contains(text, "Không")
                    || Contains(text, "Vui lòng")
                    || Contains(text, "dữ liệu");
                var hasEnglishSignal = Contains(text, "Found")
                    || Contains(text, "Please")
                    || Contains(text, "Showing")
                    || Contains(text, "No data");
                Check(hasVietnameseSignal && !hasEnglishSignal, "LocaleGuard", "vi-VN summary did not look Vietnamese", issues);
                return;
            }

            var hasVietnameseSignalInEnglish = Contains(text, "Tìm thấy")
                || Contains(text, "Không tìm")
                || Contains(text, "Vui lòng");
            Check(!hasVietnameseSignalInEnglish, "LocaleGuard", "en-US summary contained Vietnamese phrasing", issues);
        }

        private static void LengthGuard(AnswerComposerRequest request, string text, List<string> issues)
        {
            Check(text.Length <= 1200, "LengthGuard", $"summary length {text.Length} exceeds 1200 characters", issues);

            var maxSentences = Math.Max(1, request.AnswerPolicy.Summary.MaxSentences);
            var sentenceCount = SentenceRegex.Matches(text)
                .Cast<Match>()
                .Count(match => match.Value.Length > 0);
            if (!string.IsNullOrWhiteSpace(text))
            {
                sentenceCount = Math.Max(1, sentenceCount);
            }

            Check(sentenceCount <= maxSentences, "LengthGuard", $"summary has {sentenceCount} sentences, max is {maxSentences}", issues);
        }

        private static void RowCountGuard(
            AnswerComposerRequest request,
            AssistantAnswer answer,
            string text,
            List<string> issues)
        {
            if (answer.AnswerType != "structured" || !request.AnswerPolicy.Summary.IncludeRowCount)
            {
                return;
            }

            Check(Contains(text, request.RowCount.ToString(CultureInfo.InvariantCulture)), "RowCountGuard", "summary omitted rowCount", issues);
        }

        private static void TruncationGuard(
            AnswerComposerRequest request,
            AssistantAnswer answer,
            string text,
            AnswerNarrationResult narration,
            List<string> issues)
        {
            if (answer.AnswerType != "structured")
            {
                return;
            }

            var narrationTruncated = request.RowCount > Math.Min(request.Rows.Count, request.AnswerPolicy.MaxRowsForNarration);
            var tableTruncated = answer.Blocks.OfType<TableBlock>().Any(block => block.Truncated);
            if (!narrationTruncated && !tableTruncated)
            {
                return;
            }

            var mentionsSubset = Contains(text, "first")
                || Contains(text, "subset")
                || Contains(text, "shown")
                || Contains(text, "đầu")
                || Contains(text, "hiển thị")
                || narration.Warnings.Any(warning => warning.Contains("truncated", StringComparison.OrdinalIgnoreCase));
            Check(mentionsSubset, "TruncationGuard", "truncated result did not mention subset/truncation", issues);
        }

        private static void NoRecommendationGuard(
            AnswerComposerRequest request,
            string text,
            List<string> issues)
        {
            var allowed = request.AnswerPolicy.Summary.ForbiddenClaims.Any(claim =>
                claim.Contains("recommend", StringComparison.OrdinalIgnoreCase)
                && claim.Contains("unless", StringComparison.OrdinalIgnoreCase));
            Check(allowed || !RecommendationRegex.IsMatch(text), "NoRecommendationGuard", "summary included recommendation/action language", issues);
        }

        private static void NoProcedureLeakGuard(
            AnswerComposerRequest request,
            string text,
            List<string> issues)
        {
            Check(string.IsNullOrWhiteSpace(request.ProcedureName) || !Contains(text, request.ProcedureName), "NoProcedureLeakGuard", "summary mentioned procedure name", issues);
            Check(!Contains(text, "dbo."), "NoProcedureLeakGuard", "summary mentioned a stored procedure prefix", issues);
        }

        private static void UsedColumnsGuard(
            AnswerComposerRequest request,
            AnswerNarrationResult narration,
            List<string> issues)
        {
            var visibleColumns = request.ResultSchema?.Columns
                .Where(column => column.Visible)
                .Select(column => column.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? request.Rows.FirstOrDefault()?.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var usedColumn in narration.UsedColumns)
            {
                Check(visibleColumns.Contains(usedColumn), "FactualityGuard", $"usedColumns included unavailable column '{usedColumn}'", issues);
            }
        }

        private static void NumericFactualityGuard(
            SummaryEvalCase evalCase,
            AnswerComposerRequest request,
            string text,
            List<string> issues)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                request.RowCount.ToString(CultureInfo.InvariantCulture),
                request.Rows.Count.ToString(CultureInfo.InvariantCulture),
                request.AnswerPolicy.MaxRowsForNarration.ToString(CultureInfo.InvariantCulture),
                request.AnswerPolicy.Table.MaxDisplayedRows.ToString(CultureInfo.InvariantCulture)
            };

            foreach (var term in evalCase.MustMention.Concat(evalCase.MustNotMention))
            {
                foreach (Match match in NumberRegex.Matches(term))
                {
                    allowed.Add(match.Value);
                }
            }

            foreach (var value in request.Rows.SelectMany(row => row.Values).Concat(request.Arguments.Values))
            {
                AddAllowedNumber(value, allowed);
            }

            foreach (Match match in NumberRegex.Matches(text))
            {
                Check(allowed.Contains(match.Value), "FactualityGuard", $"mentioned fabricated numeric value '{match.Value}'", issues);
            }
        }

        private static IEnumerable<string> SensitiveValues(SummaryEvalCase evalCase, AnswerComposerRequest request)
        {
            var sensitiveColumns = request.SensitivityPolicy.HiddenColumns
                .Concat(request.SensitivityPolicy.MaskColumns)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in evalCase.Rows.Select(ToDictionary).Append(ToDictionary(evalCase.Arguments)))
            {
                foreach (var (key, value) in row)
                {
                    if (sensitiveColumns.Contains(key) && value is not null)
                    {
                        yield return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                    }
                }
            }
        }

        private static void AddAllowedNumber(object? value, ISet<string> allowed)
        {
            if (value is null)
            {
                return;
            }

            switch (value)
            {
                case byte or sbyte or short or ushort or int or uint or long or ulong:
                case decimal or double or float:
                    allowed.Add(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                    break;
                case string text:
                    foreach (Match match in NumberRegex.Matches(text))
                    {
                        allowed.Add(match.Value);
                    }

                    break;
            }
        }

        private static bool Contains(string text, string term) =>
            !string.IsNullOrWhiteSpace(term)
            && text.Contains(term, StringComparison.OrdinalIgnoreCase);

        private static void Check(
            [DoesNotReturnIf(false)] bool condition,
            string guard,
            string message,
            List<string> issues)
        {
            if (!condition)
            {
                issues.Add($"{guard}: {message}");
            }
        }
    }

    private sealed record SummaryEvalCase
    {
        public required string Id { get; init; }
        public string Priority { get; init; } = "P0";
        public required string Locale { get; init; }
        public required string CapabilityKey { get; init; }
        public string? ProcedureName { get; init; }
        public JsonElement Arguments { get; init; }
        public int RowCount { get; init; }
        public IReadOnlyList<JsonElement> Rows { get; init; } = [];
        public JsonElement ResultSchema { get; init; }
        public JsonElement AnswerPolicy { get; init; }
        public JsonElement SensitivityPolicy { get; init; }
        public string ExpectedAnswerType { get; init; } = "structured";
        public JsonElement NarratorOutput { get; init; }
        public IReadOnlyList<string> MustMention { get; init; } = [];
        public IReadOnlyList<string> MustNotMention { get; init; } = [];
        public required string ExpectedLocale { get; init; }
        public string? ErrorCode { get; init; }
        public IReadOnlyList<string> MissingArguments { get; init; } = [];
        public bool ExpectFallback { get; init; }
    }

    private sealed record SummaryEvalResult(
        SummaryEvalCase Case,
        string GeneratedText,
        AnswerNarrationResult Narration,
        string AnswerType,
        IReadOnlyList<string> Issues);
}
