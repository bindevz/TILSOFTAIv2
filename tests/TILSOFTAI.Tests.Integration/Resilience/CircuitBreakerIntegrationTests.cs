using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TILSOFTAI.Domain.Resilience;
using TILSOFTAI.Infrastructure.Resilience;
using Xunit;

namespace TILSOFTAI.Tests.Integration.Resilience;

public class CircuitBreakerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CircuitBreakerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CircuitBreaker_Should_Expose_Status_Endpoint()
    {
        var client = _factory.CreateClient();

        // Ensure at least one circuit is created (e.g. by making a call or manually accessing registry via scope if possible, 
        // but easier to just check endpoint returns 200 and some JSON)
        
        var response = await client.GetAsync("/health/circuits");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("circuits");
    }

    // Checking actual circuit breaking in integration test is hard without mocking the LLM/External service failure persistently.
    // Since we don't have a mock server for LLM in this quick test (unless we setup WireMock), 
    // we will rely on Unit tests for logic and just verify wiring here.
}
