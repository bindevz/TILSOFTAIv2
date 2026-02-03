using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Metrics;
using Xunit;

namespace TILSOFTAI.Tests.Unit.Metrics;

public class MetricsServiceTests
{
    private readonly Mock<IOptions<MetricsOptions>> _optionsMock;
    private readonly MetricsOptions _options;

    public MetricsServiceTests()
    {
        _options = new MetricsOptions { Enabled = true };
        _optionsMock = new Mock<IOptions<MetricsOptions>>();
        _optionsMock.Setup(x => x.Value).Returns(_options);
    }

    [Fact]
    public void IncrementCounter_ShouldNotThrow_WhenEnabled()
    {
        var service = new PrometheusMetricsService(_optionsMock.Object);
        var act = () => service.IncrementCounter("test_counter");
        act.Should().NotThrow();
    }

    [Fact]
    public void IncrementCounter_WithLabels_ShouldNotThrow()
    {
        var service = new PrometheusMetricsService(_optionsMock.Object);
        var act = () => service.IncrementCounter("test_counter_labeled", new Dictionary<string, string> { { "label", "value" } });
        act.Should().NotThrow();
    }
    
    [Fact]
    public void CreateTimer_ShouldReturnDisposable()
    {
        var service = new PrometheusMetricsService(_optionsMock.Object);
        using var timer = service.CreateTimer("test_timer");
        timer.Should().NotBeNull();
        timer.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public void Disabled_ShouldDoNothing_ButNotThrow()
    {
        _options.Enabled = false;
        var service = new PrometheusMetricsService(_optionsMock.Object);
        var act = () => service.IncrementCounter("test_counter");
        act.Should().NotThrow();
    }
}
