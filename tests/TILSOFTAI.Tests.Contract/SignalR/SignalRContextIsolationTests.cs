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
        // Arrange: Create two SignalR connections with different tenants
        var client1 = _factory.CreateClient();
        var connection1 = new HubConnectionBuilder()
            .WithUrl($"{client1.BaseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_A");
                options.Headers.Add("X-Test-User", "USER_1");
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        var client2 = _factory.CreateClient();
        var connection2 = new HubConnectionBuilder()
            .WithUrl($"{client2.BaseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_B");
                options.Headers.Add("X-Test-User", "USER_2");
                options.Headers.Add("X-Test-Roles", "User");
            })
            .Build();

        try
        {
            // Act: Start both connections and invoke EchoContext concurrently
            await connection1.StartAsync();
            await connection2.StartAsync();

            var task1 = connection1.InvokeAsync<dynamic>("EchoContext");
            var task2 = connection2.InvokeAsync<dynamic>("EchoContext");

            await Task.WhenAll(task1, task2);

            var result1 = await task1;
            var result2 = await task2;

            // Assert: Each connection should have its own isolated context
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            
            Assert.Equal("TENANT_A", (string)result1.TenantId);
            Assert.Equal("USER_1", (string)result1.UserId);
            
            Assert.Equal("TENANT_B", (string)result2.TenantId);
            Assert.Equal("USER_2", (string)result2.UserId);
            
            // Verify no context bleed - tenants should be different
            Assert.NotEqual((string)result1.TenantId, (string)result2.TenantId);
            Assert.NotEqual((string)result1.UserId, (string)result2.UserId);
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
        // Arrange: Create a single connection with specific tenant
        var client = _factory.CreateClient();
        var connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_STABLE");
                options.Headers.Add("X-Test-User", "USER_STABLE");
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        try
        {
            await connection.StartAsync();

            // Act: Invoke EchoContext multiple times
            var result1 = await connection.InvokeAsync<dynamic>("EchoContext");
            var result2 = await connection.InvokeAsync<dynamic>("EchoContext");
            var result3 = await connection.InvokeAsync<dynamic>("EchoContext");

            // Assert: All invocations should maintain same context
            Assert.Equal("TENANT_STABLE", (string)result1.TenantId);
            Assert.Equal("TENANT_STABLE", (string)result2.TenantId);
            Assert.Equal("TENANT_STABLE", (string)result3.TenantId);
            
            Assert.Equal("USER_STABLE", (string)result1.UserId);
            Assert.Equal("USER_STABLE", (string)result2.UserId);
            Assert.Equal("USER_STABLE", (string)result3.UserId);
        }
        finally
        {
            await connection.StopAsync();
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task SequentialConnections_DifferentTenants_IndependentContexts()
    {
        // Arrange & Act: Create two connections sequentially with different tenants
        dynamic result1;
        {
            var connection1 = new HubConnectionBuilder()
                .WithUrl($"{_factory.CreateClient().BaseAddress}hubs/chat", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Test-Tenant", "TENANT_SEQ_1");
                    options.Headers.Add("X-Test-User", "USER_SEQ_1");
                    options.Headers.Add("X-Test-Roles", "Admin");
                })
                .Build();

            await connection1.StartAsync();
            result1 = await connection1.InvokeAsync<dynamic>("EchoContext");
            await connection1.StopAsync();
            await connection1.DisposeAsync();
        }

        dynamic result2;
        {
            var connection2 = new HubConnectionBuilder()
                .WithUrl($"{_factory.CreateClient().BaseAddress}hubs/chat", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Test-Tenant", "TENANT_SEQ_2");
                    options.Headers.Add("X-Test-User", "USER_SEQ_2");
                    options.Headers.Add("X-Test-Roles", "User");
                })
                .Build();

            await connection2.StartAsync();
            result2 = await connection2.InvokeAsync<dynamic>("EchoContext");
            await connection2.StopAsync();
            await connection2.DisposeAsync();
        }

        // Assert: Each connection should have had its own context
        Assert.Equal("TENANT_SEQ_1", (string)result1.TenantId);
        Assert.Equal("USER_SEQ_1", (string)result1.UserId);
        
        Assert.Equal("TENANT_SEQ_2", (string)result2.TenantId);
        Assert.Equal("USER_SEQ_2", (string)result2.UserId);
    }
}
