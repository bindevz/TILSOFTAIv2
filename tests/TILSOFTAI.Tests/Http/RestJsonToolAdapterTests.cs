using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Secrets;
using TILSOFTAI.Infrastructure.Http;
using TILSOFTAI.Tools.Abstractions;
using Xunit;

namespace TILSOFTAI.Tests.Http;

public sealed class RestJsonToolAdapterTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnBindingInvalid_WhenEndpointMissing()
    {
        var adapter = new RestJsonToolAdapter(new HttpClient(new SequenceHandler()));

        var result = await adapter.ExecuteAsync(CreateRequest(new Dictionary<string, string?>()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("REST_BINDING_INVALID");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRetryTransientHttpFailure_ThenSucceed()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("temporary")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            });
        var adapter = new RestJsonToolAdapter(new HttpClient(handler));

        var result = await adapter.ExecuteAsync(CreateRequest(new Dictionary<string, string?>
        {
            ["baseUrl"] = "https://inventory.local",
            ["endpoint"] = "/stock",
            ["method"] = "GET",
            ["retryCount"] = "1",
            ["retryDelayMs"] = "0"
        }), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PayloadJson.Should().Contain("ok");
        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldClassifyClientHttpFailure()
    {
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("missing")
        });
        var adapter = new RestJsonToolAdapter(new HttpClient(handler));

        var result = await adapter.ExecuteAsync(CreateRequest(new Dictionary<string, string?>
        {
            ["baseUrl"] = "https://inventory.local",
            ["endpoint"] = "/stock",
            ["method"] = "GET"
        }), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("REST_CLIENT_ERROR");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectRawSecretMetadata()
    {
        var adapter = new RestJsonToolAdapter(new HttpClient(new SequenceHandler()));

        var result = await adapter.ExecuteAsync(CreateRequest(new Dictionary<string, string?>
        {
            ["baseUrl"] = "https://inventory.local",
            ["endpoint"] = "/stock",
            ["method"] = "GET",
            ["authToken"] = "token-1"
        }), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("REST_SECRET_POLICY_VIOLATION");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldApplySecretBackedAuthAndApiKeyFromConnectionCatalog()
    {
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });
        var secretProvider = new FakeSecretProvider(new Dictionary<string, string>
        {
            ["tilsoft/external/token"] = "token-1",
            ["tilsoft/external/key"] = "key-1"
        });
        var catalog = new ConfigurationExternalConnectionCatalog(Options.Create(new ExternalConnectionCatalogOptions
        {
            Connections = new Dictionary<string, ExternalConnectionOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["external-stock-api"] = new()
                {
                    BaseUrl = "https://inventory.local",
                    AuthScheme = "Bearer",
                    AuthTokenSecret = "tilsoft/external/token",
                    ApiKeyHeader = "X-Api-Key",
                    ApiKeySecret = "tilsoft/external/key"
                }
            }
        }));
        var adapter = new RestJsonToolAdapter(new HttpClient(handler), catalog, secretProvider);

        var result = await adapter.ExecuteAsync(CreateRequest(new Dictionary<string, string?>
        {
            ["connectionName"] = "external-stock-api",
            ["endpoint"] = "/stock",
            ["method"] = "GET"
        }), CancellationToken.None);

        result.Success.Should().BeTrue();
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("token-1");
        handler.LastRequest.Headers.GetValues("X-Api-Key").Single().Should().Be("key-1");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFailWhenSecretIsMissing()
    {
        var catalog = new ConfigurationExternalConnectionCatalog(Options.Create(new ExternalConnectionCatalogOptions
        {
            Connections = new Dictionary<string, ExternalConnectionOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["external-stock-api"] = new()
                {
                    BaseUrl = "https://inventory.local",
                    AuthScheme = "Bearer",
                    AuthTokenSecret = "tilsoft/external/missing"
                }
            }
        }));
        var adapter = new RestJsonToolAdapter(
            new HttpClient(new SequenceHandler()),
            catalog,
            new FakeSecretProvider(new Dictionary<string, string>()));

        var result = await adapter.ExecuteAsync(CreateRequest(new Dictionary<string, string?>
        {
            ["connectionName"] = "external-stock-api",
            ["endpoint"] = "/stock"
        }), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("REST_SECRET_NOT_FOUND");
    }

    private static ToolExecutionRequest CreateRequest(IReadOnlyDictionary<string, string?> metadata) => new()
    {
        TenantId = "tenant-1",
        AgentId = "warehouse",
        SystemId = "external-stock-api",
        CapabilityKey = "warehouse.external-stock.lookup",
        Operation = ToolAdapterOperationNames.ExecuteHttpJson,
        ArgumentsJson = "{\"@ItemNo\":\"CHAIR-001\"}",
        CorrelationId = "corr-1",
        Metadata = metadata
    };

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public int RequestCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;

            return Task.FromResult(_responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }

    private sealed class FakeSecretProvider : ISecretProvider
    {
        private readonly IReadOnlyDictionary<string, string> _secrets;

        public FakeSecretProvider(IReadOnlyDictionary<string, string> secrets)
        {
            _secrets = secrets;
        }

        public string ProviderName => "fake";

        public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            _secrets.TryGetValue(key, out var value);
            return Task.FromResult<string?>(value);
        }

        public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.ContainsKey(key));
    }
}
