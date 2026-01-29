using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Auth;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Normalization;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Tools;
using Xunit;

namespace TILSOFTAI.Tests.Integration;

public sealed class ChatEndpointTests
{
    [Fact]
    public async Task Chat_ReturnsContent()
    {
        await using var factory = new ChatApiFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new
            {
                input = "Hello",
                allowCache = true,
                containsSensitive = false
            })
        };
        request.Headers.Add("X-Tenant-Id", "tenant-1");
        request.Headers.Add("X-User-Id", "user-1");
        request.Headers.Add("X-Roles", "user");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("fake response", root.GetProperty("content").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("conversationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.Equal("en", root.GetProperty("language").GetString());
    }

    [Fact]
    public async Task ChatStream_EmitsDeltaAndFinal()
    {
        await using var factory = new ChatApiFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream")
        {
            Content = JsonContent.Create(new
            {
                input = "Hello",
                allowCache = false,
                containsSensitive = false
            })
        };
        request.Headers.Add("X-Tenant-Id", "tenant-1");
        request.Headers.Add("X-User-Id", "user-1");
        request.Headers.Add("X-Roles", "user");

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var events = new List<(string Type, string Data)>();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        string currentEvent = string.Empty;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                currentEvent = line[7..].Trim();
                continue;
            }

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var data = line[6..];
                events.Add((currentEvent, data));

                if (string.Equals(currentEvent, "final", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
        }

        Assert.Contains(events, evt => evt.Type == "delta");
        var finalEvent = events.LastOrDefault(evt => evt.Type == "final");
        Assert.False(string.IsNullOrWhiteSpace(finalEvent.Data));

        using var finalDoc = JsonDocument.Parse(finalEvent.Data);
        Assert.Equal("final", finalDoc.RootElement.GetProperty("type").GetString());
        Assert.Equal("hello world", finalDoc.RootElement.GetProperty("payload").GetString());
    }

    [Fact]
    public async Task ChatStream_HandlesManyDeltas()
    {
        await using var factory = new ChatApiFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream")
        {
            Content = JsonContent.Create(new
            {
                input = "burst",
                allowCache = false,
                containsSensitive = false
            })
        };
        request.Headers.Add("X-Tenant-Id", "tenant-1");
        request.Headers.Add("X-User-Id", "user-1");
        request.Headers.Add("X-Roles", "user");

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var deltaCount = 0;
        var finalSeen = false;
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        string currentEvent = string.Empty;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                currentEvent = line[7..].Trim();
                continue;
            }

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                if (currentEvent == "delta")
                {
                    deltaCount++;
                }

                if (currentEvent == "final")
                {
                    finalSeen = true;
                    break;
                }
            }
        }

        Assert.True(finalSeen);
        Assert.True(deltaCount > 0);
    }

    private sealed class ChatApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                });

                services.RemoveAll<ILlmClient>();
                services.AddSingleton<ILlmClient, FakeLlmClient>();

                services.RemoveAll<IToolCatalogResolver>();
                services.AddSingleton<IToolCatalogResolver, FakeToolCatalogResolver>();

                services.RemoveAll<INormalizationRuleProvider>();
                services.AddSingleton<INormalizationRuleProvider, FakeNormalizationRuleProvider>();

                services.RemoveAll<IContextPackProvider>();
                services.AddSingleton<IContextPackProvider, FakeContextPackProvider>();
            });
        }
    }

    private sealed class FakeLlmClient : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(LlmRequest req, CancellationToken ct)
        {
            return Task.FromResult(new LlmResponse { Content = "fake response" });
        }

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest req, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            var lastUser = req.Messages.LastOrDefault(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (string.Equals(lastUser?.Content, "burst", StringComparison.OrdinalIgnoreCase))
            {
                for (var i = 0; i < 400; i++)
                {
                    yield return LlmStreamEvent.Delta("x");
                }
                yield return LlmStreamEvent.Final("done");
                yield break;
            }

            yield return LlmStreamEvent.Delta("hello ");
            yield return LlmStreamEvent.Final("world");
            await Task.CompletedTask;
        }
    }

    private sealed class FakeToolCatalogResolver : IToolCatalogResolver
    {
        public Task<IReadOnlyList<ToolDefinition>> GetResolvedToolsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ToolDefinition>>(Array.Empty<ToolDefinition>());
        }
    }

    private sealed class FakeNormalizationRuleProvider : INormalizationRuleProvider
    {
        public Task<IReadOnlyList<NormalizationRuleRecord>> GetRulesAsync(string tenantId, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<NormalizationRuleRecord>>(Array.Empty<NormalizationRuleRecord>());
        }
    }

    private sealed class FakeContextPackProvider : IContextPackProvider
    {
        public Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(TilsoftExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new System.Security.Claims.ClaimsIdentity("Test");
            identity.AddClaim(new System.Security.Claims.Claim(TilsoftClaims.TenantId, "tenant-1"));
            identity.AddClaim(new System.Security.Claims.Claim(TilsoftClaims.UserId, "user-1"));
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
