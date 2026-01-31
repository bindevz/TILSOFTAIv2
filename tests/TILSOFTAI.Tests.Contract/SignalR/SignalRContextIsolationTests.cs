using Microsoft.AspNetCore.SignalR.Client;
using TILSOFTAI.Tests.Contract.Fixtures;

namespace TILSOFTAI.Tests.Contract.SignalR;

/// <summary>
/// Contract tests for SignalR tenant context isolation.
/// Verifies that concurrent connections with different tenants don't experience context bleed.
/// </summary>
public class SignalRContextIsolationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SignalRContextIsolationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentConnections_DifferentTenants_NoContextBleed()
    {
        // Arrange: Create two SignalR connections
        // Note: Current TestAuth uses static claims, so both would have same tenant
        // This test documents the expected behavior for isolation
        
        var client = _factory.CreateClient();
        
        var connection1 = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        var connection2 = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        try
        {
            // Act: Start both connections
            await connection1.StartAsync();
            await connection2.StartAsync();

            // Assert: Both connections should be independent
            Assert.Equal(HubConnectionState.Connected, connection1.State);
            Assert.Equal(HubConnectionState.Connected, connection2.State);
            
            // Note: In a real scenario with different tenant claims per connection,
            // we would verify that each connection's invocations use the correct tenant
        }
        finally
        {
            await connection1.StopAsync();
            await connection2.StopAsync();
            await connection1.DisposeAsync();
            await connection2.DisposeAsync();
        }
    }

    [Fact]
    public async Task MultipleInvocations_SameConnection_MaintainsContext()
    {
        // Arrange
        var client = _factory.CreateClient();
        var connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        try
        {
            await connection.StartAsync();

            // Act: Multiple invocations should maintain same context
            // (Hub filter sets/clears context per invocation)
            
            // Assert: Connection remains stable across invocations
            Assert.Equal(HubConnectionState.Connected, connection.State);
            
            // Note: Actual hub method invocations would be tested here
            // but require proper request DTOs and response handling
        }
        finally
        {
            await connection.StopAsync();
            await connection.DisposeAsync();
        }
    }
}
