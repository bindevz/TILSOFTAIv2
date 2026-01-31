using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using TILSOFTAI.Tests.Contract.Fixtures;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Security;

public class CorsContractTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CorsContractTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OptionsRequest_WithAllowedOrigin_ReturnsAllowOriginHeader()
    {
        // Arrange
        var allowedOrigin = "https://example.test";
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cors:Enabled"] = "true",
                    ["Cors:AllowedOrigins:0"] = allowedOrigin
                });
            });
        }).CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", allowedOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.ServiceUnavailable, 
            $"Expected OK/ServiceUnavailable, got {response.StatusCode}"
        );
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"), "Missing Access-Control-Allow-Origin header");
        var originHeader = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        Assert.Equal(allowedOrigin, originHeader);
    }

    [Fact]
    public async Task OptionsRequest_WithDisallowedOrigin_DoesNotReturnAllowOriginHeader()
    {
        // Arrange
        var allowedOrigin = "https://example.test";
        var disallowedOrigin = "https://evil.test";
        
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cors:Enabled"] = "true",
                    ["Cors:AllowedOrigins:0"] = allowedOrigin
                });
            });
        }).CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", disallowedOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected OK/ServiceUnavailable, got {response.StatusCode}"
        );
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"), "Should not return Access-Control-Allow-Origin for disallowed origin");
    }
}
