using System.Text.Json;
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
    [InlineData("write-preview.jsonl")]
    public void EvalDataset_ShouldExistAndContainValidJsonLines(string fileName)
    {
        var path = Path.Combine(RepositoryRoot, "tests", "TILSOFTAI.Evals", fileName);

        File.Exists(path).Should().BeTrue($"{fileName} is a Sprint 31 phase 8 acceptance artifact");
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
}
