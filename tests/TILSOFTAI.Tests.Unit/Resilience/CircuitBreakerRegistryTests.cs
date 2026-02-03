using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Infrastructure.Resilience;
using Xunit;

namespace TILSOFTAI.Tests.Unit.Resilience;

public class CircuitBreakerRegistryTests
{
    private readonly Mock<IMetricsService> _metricsMock;
    private readonly ResilienceOptions _options;
    private readonly CircuitBreakerRegistry _registry;

    public CircuitBreakerRegistryTests()
    {
        _metricsMock = new Mock<IMetricsService>();
        _options = new ResilienceOptions();
        var optionsMock = new Mock<IOptions<ResilienceOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);

        _registry = new CircuitBreakerRegistry(new NullLoggerFactory(), _metricsMock.Object, optionsMock.Object);
    }

    [Fact]
    public void GetOrCreate_Should_Return_Same_Instance_For_Same_Name()
    {
        var cb1 = _registry.GetOrCreate("test");
        var cb2 = _registry.GetOrCreate("test");

        cb1.Should().BeSameAs(cb2);
    }

    [Fact]
    public void GetOrCreate_Should_Return_Different_Instances_For_Different_Names()
    {
        var cb1 = _registry.GetOrCreate("test1");
        var cb2 = _registry.GetOrCreate("test2");

        cb1.Should().NotBeSameAs(cb2);
    }

    [Fact]
    public void GetAllStates_Should_Return_All_Circuits()
    {
        _registry.GetOrCreate("test1");
        _registry.GetOrCreate("test2");

        var states = _registry.GetAllStates();
        states.Should().HaveCount(2);
        states.Should().ContainKey("test1");
        states.Should().ContainKey("test2");
    }
}
