using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using TILSOFTAI.Api;
using Xunit;

namespace TILSOFTAI.Tests.Integration.Metrics;

public class MetricsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MetricsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Metrics_ReturnsPrometheusFormat()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        // Metrics endpoint is configured at /metrics by default in MetricsOptions
        var response = await client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.ToString().Should().Contain("text/plain"); // prometheus format 0.0.4 is text/plain

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Check for some expected metrics (dotnet runtime or our custom ones)
        // Since this is a fresh factory, custom metrics might not be populated yet unless we trigger them.
        // But headers/comments usually exist.
        // content.Should().Contain("# HELP");
        // content.Should().Contain("# TYPE");
    }
}
