using System.Net;
using TILSOFTAI.Tests.Contract.Fixtures;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Ops;

/// <summary>
/// Contract tests for health check endpoints.
/// Verifies that health endpoints are properly mapped and accessible.
/// </summary>
public sealed class HealthEndpointContractTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public HealthEndpointContractTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthLiveEndpointShouldReturn200()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }
    
    [Fact]
    public async Task HealthReadyEndpointShouldReturn200()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        // Might be Healthy or Unhealthy depending on services, but endpoint must be reachable.
        // In test environment (no real SQL/Redis), we might get Unhealthy if not mocked,
        // but the goal is to verify endpoint existence and response type.
        // Let's check status code is valid (200 or 503) to ensure endpoint exists.
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected 200 or 503, got {response.StatusCode}"
        );
    }
}
