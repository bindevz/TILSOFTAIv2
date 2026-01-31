using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using TILSOFTAI.Tests.Contract.Fixtures;
using TILSOFTAI.Api.Hubs;

namespace TILSOFTAI.Tests.Contract.SignalR;

/// <summary>
/// Contract tests for SignalR claims enforcement and fail-closed behavior.
/// Verifies that hub invocations require valid tenant and user claims.
/// </summary>
public class SignalRClaimsEnforcementTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HubConnection? _connection;

    public SignalRClaimsEnforcementTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartChat_WithValidClaims_Succeeds()
    {
        // Arrange: Create SignalR connection with valid claims (using default TestAuth)
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", "TENANT_A");
        client.DefaultRequestHeaders.Add("X-Test-User", "USER_1");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        _connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_A");
                options.Headers.Add("X-Test-User", "USER_1");
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        await _connection.StartAsync();

        // Act & Assert: Invoke EchoContext to verify claims are present
        var result = await _connection.InvokeAsync<dynamic>("EchoContext");
        
        Assert.NotNull(result);
        Assert.Equal("TENANT_A", (string)result.TenantId);
        Assert.Equal("USER_1", (string)result.UserId);

        await _connection.StopAsync();
    }

    [Fact]
    public async Task EchoContext_MissingTenantClaim_ThrowsUnauthenticated()
    {
        // Arrange: Create connection without tenant claim (empty string)
        var client = _factory.CreateClient();
        _connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", ""); // Empty tenant
                options.Headers.Add("X-Test-User", "USER_1");
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        await _connection.StartAsync();

        // Act & Assert: Invoking EchoContext should throw HubException
        var exception = await Assert.ThrowsAsync<HubException>(async () =>
        {
            await _connection.InvokeAsync<dynamic>("EchoContext");
        });

        Assert.Contains("UNAUTHENTICATED", exception.Message.ToUpperInvariant());

        await _connection.StopAsync();
    }

    [Fact]
    public async Task EchoContext_MissingUserClaim_ThrowsUnauthenticated()
    {
        // Arrange: Create connection without user claim (empty string)
        var client = _factory.CreateClient();
        _connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_A");
                options.Headers.Add("X-Test-User", ""); // Empty user
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        await _connection.StartAsync();

        // Act & Assert: Invoking EchoContext should throw HubException
        var exception = await Assert.ThrowsAsync<HubException>(async () =>
        {
            await _connection.InvokeAsync<dynamic>("EchoContext");
        });

        Assert.Contains("UNAUTHENTICATED", exception.Message.ToUpperInvariant());

        await _connection.StopAsync();
    }
}
