using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Orchestration.Observability;
using Xunit;

namespace TILSOFTAI.Tests.Observability;

public sealed class RuntimeExecutionInstrumentationTests
{
    [Fact]
    public void BridgeFallbackObservation_ShouldIncrementFallbackCounterWithPathLabels()
    {
        var metrics = new CapturingMetricsService();
        var instrumentation = new RuntimeExecutionInstrumentation(
            metrics,
            new Mock<ILogger<RuntimeExecutionInstrumentation>>().Object);

        instrumentation.RecordBridgeFallback(
            "warehouse",
            "no_capability_match",
            TimeSpan.FromMilliseconds(25),
            success: true);

        metrics.Counters.Should().Contain(c =>
            c.Name == MetricNames.RuntimeBridgeFallbackTotal
            && c.Labels["agent"] == "warehouse"
            && c.Labels["reason"] == "no_capability_match"
            && c.Labels["success"] == "true");

        metrics.Histograms.Should().Contain(h =>
            h.Name == MetricNames.RuntimeExecutionDurationSeconds
            && h.Labels["path"] == "bridge");
    }

    [Fact]
    public void AdapterFailureObservation_ShouldIncrementAdapterFailureCounter()
    {
        var metrics = new CapturingMetricsService();
        var instrumentation = new RuntimeExecutionInstrumentation(
            metrics,
            new Mock<ILogger<RuntimeExecutionInstrumentation>>().Object);

        instrumentation.RecordAdapterFailure("warehouse", "warehouse.external-stock.lookup", "rest-json", "REST_HTTP_ERROR");

        metrics.Counters.Should().Contain(c =>
            c.Name == MetricNames.RuntimeAdapterFailuresTotal
            && c.Labels["adapter"] == "rest-json"
            && c.Labels["capability"] == "warehouse.external-stock.lookup"
            && c.Labels["error"] == "rest_http_error");
    }

    private sealed class CapturingMetricsService : IMetricsService
    {
        public List<(string Name, Dictionary<string, string> Labels, double Value)> Counters { get; } = new();
        public List<(string Name, Dictionary<string, string> Labels, double Value)> Histograms { get; } = new();

        public void IncrementCounter(string name, Dictionary<string, string>? labels = null, double value = 1)
        {
            Counters.Add((name, labels ?? new Dictionary<string, string>(), value));
        }

        public void RecordHistogram(string name, double value, Dictionary<string, string>? labels = null)
        {
            Histograms.Add((name, labels ?? new Dictionary<string, string>(), value));
        }

        public void RecordGauge(string name, double value, Dictionary<string, string>? labels = null)
        {
        }

        public IDisposable CreateTimer(string name, Dictionary<string, string>? labels = null) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
