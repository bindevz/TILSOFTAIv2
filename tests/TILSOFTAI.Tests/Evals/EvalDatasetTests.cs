using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace TILSOFTAI.Tests.Evals;

public sealed class EvalDatasetTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Theory]
    [InlineData("tool-routing.vi.jsonl")]
    [InlineData("tool-routing.en.jsonl")]
    [InlineData("argument-extraction.vi.jsonl")]
    [InlineData("argument-extraction.en.jsonl")]
    [InlineData("answer-composer.jsonl")]
    [InlineData("answer-summary/model-summary-eval.jsonl")]
    [InlineData("write-preview.jsonl")]
    [InlineData("sprint32-agent-framework-regressions.jsonl")]
    public void EvalDataset_ShouldExistAndContainValidJsonLines(string fileName)
    {
        var path = Path.Combine(RepositoryRoot, "tests", "TILSOFTAI.Evals", fileName);

        File.Exists(path).Should().BeTrue($"{fileName} is an Agent Framework routing eval artifact");
        var lines = File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        lines.Should().NotBeEmpty();

        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            document.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        }
    }

    [Theory]
    [InlineData("tool-routing.vi.jsonl")]
    [InlineData("tool-routing.en.jsonl")]
    [InlineData("argument-extraction.vi.jsonl")]
    [InlineData("argument-extraction.en.jsonl")]
    public void RoutingEvalDataset_ShouldDeclareLocaleFunctionAndExpectedOutcome(string fileName)
    {
        var path = Path.Combine(RepositoryRoot, "tests", "TILSOFTAI.Evals", fileName);

        foreach (var line in File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            root.TryGetProperty("utterance", out _).Should().BeTrue();
            root.TryGetProperty("locale", out _).Should().BeTrue();
            root.TryGetProperty("expectedFunction", out _).Should().BeTrue();
            (root.TryGetProperty("expectedArguments", out _)
                || root.TryGetProperty("expectedClarification", out _)).Should().BeTrue();
        }
    }

    [Fact]
    public void Sprint31RoutingEvalNames_ShouldMatchSqlBackedCatalogFunctionNames()
    {
        var catalogFunctions = ReadSqlCapabilityFunctionNames();
        var routingFiles = new[]
        {
            "tool-routing.vi.jsonl",
            "tool-routing.en.jsonl",
            "argument-extraction.vi.jsonl",
            "argument-extraction.en.jsonl"
        };

        var offenders = routingFiles
            .SelectMany(fileName => ReadJsonLines(fileName)
                .Select(root => new
                {
                    FileName = fileName,
                    FunctionName = root.GetProperty("expectedFunction").GetString() ?? string.Empty
                }))
            .Where(item => !catalogFunctions.Contains(item.FunctionName)
                && !item.FunctionName.EndsWith("_preview", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{item.FileName}: {item.FunctionName}")
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("Sprint 32 requires Sprint 31 eval names to align with SQL-backed catalog function names");
    }

    [Fact]
    public void Sprint32RegressionDataset_ShouldCatchKnownAgentFrameworkFailureModes()
    {
        var cases = ReadJsonLines("sprint32-agent-framework-regressions.jsonl").ToArray();
        cases.Should().NotBeEmpty();

        var regressionTypes = cases
            .Select(root => root.GetProperty("regressionType").GetString())
            .ToArray();

        regressionTypes.Should().Contain("first_tool_selection");
        regressionTypes.Should().Contain("regex_only_extraction");
        regressionTypes.Should().Contain("over_exposed_tools");
        regressionTypes.Should().Contain("write_safety");

        cases.Where(root => root.GetProperty("regressionType").GetString() == "first_tool_selection")
            .Any(HasFirstToolSelectionGuard)
            .Should()
            .BeTrue();

        cases.Where(root => root.GetProperty("regressionType").GetString() == "regex_only_extraction")
            .Any(HasRegexOnlyExtractionGuard)
            .Should()
            .BeTrue();

        cases.Where(root => root.GetProperty("regressionType").GetString() == "over_exposed_tools")
            .Any(HasOverExposedToolsGuard)
            .Should()
            .BeTrue();

        cases.Where(root => root.GetProperty("regressionType").GetString() == "write_safety")
            .Any(HasWriteSafetyGuard)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Sprint32QualityGates_ShouldDefineRequiredThresholdsAndTelemetryFields()
    {
        var path = Path.Combine(RepositoryRoot, "tests", "TILSOFTAI.Evals", "sprint32-quality-gates.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var gates = root.GetProperty("gates");

        gates.GetProperty("function_selection_accuracy").GetProperty("target").GetDouble().Should().BeGreaterThanOrEqualTo(0.90);
        gates.GetProperty("argument_extraction_accuracy").GetProperty("target").GetDouble().Should().BeGreaterThanOrEqualTo(0.85);
        gates.GetProperty("missing_required_argument_detection").GetProperty("target").GetDouble().Should().BeGreaterThanOrEqualTo(0.95);
        gates.GetProperty("fake_first_tool_selection").GetProperty("target").GetInt32().Should().Be(0);
        gates.GetProperty("regex_only_extraction").GetProperty("target").GetInt32().Should().Be(0);
        gates.GetProperty("over_exposed_tools").GetProperty("target").GetInt32().Should().Be(0);
        gates.GetProperty("false_write_execution").GetProperty("target").GetInt32().Should().Be(0);

        var telemetryFields = root.GetProperty("telemetryFields")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        telemetryFields.Should().Contain([
            "advertised_function_tools",
            "selected_function",
            "arguments_before_normalization",
            "arguments_after_normalization",
            "latency_by_stage"]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "spec", "Sprint_31")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static IEnumerable<JsonElement> ReadJsonLines(string fileName)
    {
        var path = Path.Combine(RepositoryRoot, "tests", "TILSOFTAI.Evals", fileName);
        foreach (var line in File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            using var document = JsonDocument.Parse(line);
            yield return document.RootElement.Clone();
        }
    }

    private static bool HasFirstToolSelectionGuard(JsonElement root) =>
        root.TryGetProperty("candidateFunctions", out var candidates)
        && candidates.ValueKind == JsonValueKind.Array
        && candidates.GetArrayLength() > 1
        && root.TryGetProperty("mustNotSelect", out _);

    private static bool HasRegexOnlyExtractionGuard(JsonElement root) =>
        root.TryGetProperty("mustUseModelArguments", out var value)
        && value.GetBoolean()
        && root.TryGetProperty("expectedArguments", out _);

    private static bool HasOverExposedToolsGuard(JsonElement root) =>
        root.TryGetProperty("maxAdvertisedTools", out _)
        && root.TryGetProperty("disallowedAdvertisedDomains", out _);

    private static bool HasWriteSafetyGuard(JsonElement root) =>
        root.TryGetProperty("shouldExecuteWriteImmediately", out var value)
        && !value.GetBoolean()
        && root.TryGetProperty("requiresApprovedActionId", out var approval)
        && approval.GetBoolean();

    private static HashSet<string> ReadSqlCapabilityFunctionNames()
    {
        var path = Path.Combine(RepositoryRoot, "sql", "ai", "002_ai_capability_tables.sql");
        var contents = File.ReadAllText(path);
        return Regex.Matches(contents, @"N'(?<functionName>[^']+)'\s*,\s*N'sql'\s*,")
            .Select(match => match.Groups["functionName"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
