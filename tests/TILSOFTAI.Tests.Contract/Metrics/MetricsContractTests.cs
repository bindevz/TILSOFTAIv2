using FluentAssertions;
using TILSOFTAI.Domain.Metrics;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Metrics;

public class MetricsContractTests
{
    [Fact]
    public void MetricNames_ShouldAdhereToSnakeCase()
    {
        var names = typeof(MetricNames).GetFields()
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        foreach (var name in names)
        {
            name.Should().MatchRegex("^[a-z0-9_]+$", $"Metric name '{name}' should be snake_case (lowercase, numbers, underscores)");
            name.Should().StartWith("tilsoftai_", "Metric names should be namespaced with 'tilsoftai_'");
        }
    }
}
