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

public sealed class OpenAiChatCompletionsTests
{
    [Fact]
    public async Task OpenAiChatCompletions_ReturnsJson()
    {
        await using var factory = new OpenAiFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = "ignored",
                messages = new[]
                {
                    new { role = "user", content = "Hello" }
                },
                stream = false
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

        Assert.Equal("chat.completion", root.GetProperty("object").GetString());
        var choices = root.GetProperty("choices");
        Assert.True(choices.GetArrayLength() > 0);
        var content = choices[0].GetProperty("message").GetProperty("content").GetString();
        Assert.Equal("fake response", content);
    }

    [Fact]
    public async Task OpenAiChatCompletions_StreamReturnsChunks()
    {
        await using var factory = new OpenAiFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = "ignored",
                messages = new[]
                {
                    new { role = "user", content = "Hello" }
                },
                stream = true
            })
        };
        request.Headers.Add("X-Tenant-Id", "tenant-1");
        request.Headers.Add("X-User-Id", "user-1");
        request.Headers.Add("X-Roles", "user");

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var chunks = new List<string>();
        var done = false;
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var data = line[6..];
                if (data == "[DONE]")
                {
                    done = true;
                    break;
                }
                chunks.Add(data);
            }
        }

        Assert.True(done);
        Assert.NotEmpty(chunks);
        using var chunkDoc = JsonDocument.Parse(chunks[0]);
        Assert.Equal("chat.completion.chunk", chunkDoc.RootElement.GetProperty("object").GetString());
    }

    [Fact]
    public async Task OpenAiChatCompletions_StreamHandlesBurst()
    {
        await using var factory = new OpenAiFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = "ignored",
                messages = new[]
                {
                    new { role = "user", content = "burst" }
                },
                stream = true
            })
        };
        request.Headers.Add("X-Tenant-Id", "tenant-1");
        request.Headers.Add("X-User-Id", "user-1");
        request.Headers.Add("X-Roles", "user");

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dataCount = 0;
        var done = false;
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var data = line[6..];
                if (data == "[DONE]")
                {
                    done = true;
                    break;
                }

                dataCount++;
            }
        }

        Assert.True(done);
        Assert.True(dataCount > 1);
    }

    private sealed class OpenAiFactory : WebApplicationFactory<Program>
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
