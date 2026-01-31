using Microsoft.AspNetCore.SignalR.Client;
using TILSOFTAI.Tests.Contract.Fixtures;
using TILSOFTAI.Api.Contracts.Chat;

namespace TILSOFTAI.Tests.Contract.SignalR;

/// <summary>
/// Contract tests for SignalR language negotiation.
/// Verifies that language can be negotiated via querystring 'lang' and StartChat preferredLanguage.
/// </summary>
public class SignalRLanguageNegotiationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HubConnection? _connection;

    public SignalRLanguageNegotiationTests(TestWebApplicationFactory factory)
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
    public async Task EchoContext_NoLangQuerystring_ReturnsDefaultLanguage()
    {
        // Arrange: Create connection without lang querystring
        var client = _factory.CreateClient();

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

        // Act: Get execution context
        var result = await _connection.InvokeAsync<dynamic>("EchoContext");

        // Assert: Should use default language (en)
        Assert.NotNull(result);
        Assert.Equal("en", (string)result.Language);

        await _connection.StopAsync();
    }

    [Fact]
    public async Task EchoContext_ValidLangQuerystring_ReturnsNegotiatedLanguage()
    {
        // Arrange: Create connection with lang=es querystring
        var client = _factory.CreateClient();

        _connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat?lang=es", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_A");
                options.Headers.Add("X-Test-User", "USER_1");
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        await _connection.StartAsync();

        // Act: Get execution context
        var result = await _connection.InvokeAsync<dynamic>("EchoContext");

        // Assert: Should use negotiated language (es)
        Assert.NotNull(result);
        Assert.Equal("es", (string)result.Language);

        await _connection.StopAsync();
    }

    [Fact]
    public async Task EchoContext_InvalidLangQuerystring_FallbackToDefault()
    {
        // Arrange: Create connection with unsupported lang=xyz querystring
        var client = _factory.CreateClient();

        _connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat?lang=xyz", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_A");
                options.Headers.Add("X-Test-User", "USER_1");
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        await _connection.StartAsync();

        // Act: Get execution context
        var result = await _connection.InvokeAsync<dynamic>("EchoContext");

        // Assert: Should fallback to default language (en)
        Assert.NotNull(result);
        Assert.Equal("en", (string)result.Language);

        await _connection.StopAsync();
    }

    [Fact]
    public async Task EchoContext_LangQuerystringWithWhitespace_NormalizesCorrectly()
    {
        // Arrange: Create connection with lang=" es " (with whitespace)
        var client = _factory.CreateClient();

        _connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat?lang=%20es%20", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_A");
                options.Headers.Add("X-Test-User", "USER_1");
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        await _connection.StartAsync();

        // Act: Get execution context
        var result = await _connection.InvokeAsync<dynamic>("EchoContext");

        // Assert: Should normalize and use es
        Assert.NotNull(result);
        Assert.Equal("es", (string)result.Language);

        await _connection.StopAsync();
    }

    [Fact]
    public async Task StartChat_ValidPreferredLanguage_OverridesConnectionLanguage()
    {
        // Arrange: Create connection with lang=en, but request with preferredLanguage=es
        var client = _factory.CreateClient();

        _connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat?lang=en", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_A");
                options.Headers.Add("X-Test-User", "USER_1");
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        await _connection.StartAsync();

        // First verify connection language is en
        var contextBefore = await _connection.InvokeAsync<dynamic>("EchoContext");
        Assert.Equal("en", (string)contextBefore.Language);

        // Act: Call StartChat with preferredLanguage=es
        var request = new ChatApiRequest
        {
            Input = "Hello",
            PreferredLanguage = "es"
        };

        var eventReceived = false;
        _connection.On<object>("chat_event", (envelope) =>
        {
            eventReceived = true;
        });

        // Start chat (this will update the context language)
        await _connection.InvokeAsync("StartChat", request, CancellationToken.None);

        // Wait briefly for events
        await Task.Delay(500);

        // Verify context after StartChat (should be updated to es)
        var contextAfter = await _connection.InvokeAsync<dynamic>("EchoContext");

        // Assert: Language should now be es
        Assert.Equal("es", (string)contextAfter.Language);

        await _connection.StopAsync();
    }

    [Fact]
    public async Task StartChat_InvalidPreferredLanguage_KeepsConnectionLanguage()
    {
        // Arrange: Create connection with lang=es, request with invalid preferredLanguage
        var client = _factory.CreateClient();

        _connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat?lang=es", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_A");
                options.Headers.Add("X-Test-User", "USER_1");
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        await _connection.StartAsync();

        // Verify connection language is es
        var contextBefore = await _connection.InvokeAsync<dynamic>("EchoContext");
        Assert.Equal("es", (string)contextBefore.Language);

        // Act: Call StartChat with invalid preferredLanguage
        var request = new ChatApiRequest
        {
            Input = "Hello",
            PreferredLanguage = "xyz" // Invalid language
        };

        var eventReceived = false;
        _connection.On<object>("chat_event", (envelope) =>
        {
            eventReceived = true;
        });

        await _connection.InvokeAsync("StartChat", request, CancellationToken.None);

        // Wait briefly for events
        await Task.Delay(500);

        // Verify context after StartChat
        var contextAfter = await _connection.InvokeAsync<dynamic>("EchoContext");

        // Assert: Language should remain es (invalid language ignored)
        Assert.Equal("es", (string)contextAfter.Language);

        await _connection.StopAsync();
    }

    [Fact]
    public async Task StartChat_EmptyPreferredLanguage_KeepsConnectionLanguage()
    {
        // Arrange: Create connection with lang=es, request with empty preferredLanguage
        var client = _factory.CreateClient();

        _connection = new HubConnectionBuilder()
            .WithUrl($"{client.BaseAddress}hubs/chat?lang=es", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("X-Test-Tenant", "TENANT_A");
                options.Headers.Add("X-Test-User", "USER_1");
                options.Headers.Add("X-Test-Roles", "Admin");
            })
            .Build();

        await _connection.StartAsync();

        // Verify connection language is es
        var contextBefore = await _connection.InvokeAsync<dynamic>("EchoContext");
        Assert.Equal("es", (string)contextBefore.Language);

        // Act: Call StartChat with empty preferredLanguage
        var request = new ChatApiRequest
        {
            Input = "Hello",
            PreferredLanguage = "" // Empty
        };

        var eventReceived = false;
        _connection.On<object>("chat_event", (envelope) =>
        {
            eventReceived = true;
        });

        await _connection.InvokeAsync("StartChat", request, CancellationToken.None);

        // Wait briefly
        await Task.Delay(500);

        // Verify context after StartChat
        var contextAfter = await _connection.InvokeAsync<dynamic>("EchoContext");

        // Assert: Language should remain es
        Assert.Equal("es", (string)contextAfter.Language);

        await _connection.StopAsync();
    }
}
