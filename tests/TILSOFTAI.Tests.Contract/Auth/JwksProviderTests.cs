using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Auth;
using TILSOFTAI.Domain.Configuration;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Auth;

public sealed class JwksProviderTests
{
    [Fact]
    public async Task RefreshAsync_UpdatesKeys_OnSuccess()
    {
        var handler = new SequenceHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleJwksJson, Encoding.UTF8, "application/json")
            }
        });
        var provider = BuildProvider(handler);

        var success = await provider.RefreshAsync(CancellationToken.None);

        Assert.True(success);
        Assert.NotEmpty(provider.GetKeys());
    }

    [Fact]
    public async Task RefreshAsync_KeepsLastKnownKeys_OnFailure()
    {
        var handler = new SequenceHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleJwksJson, Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        });
        var provider = BuildProvider(handler);

        var first = await provider.RefreshAsync(CancellationToken.None);
        var keysAfterSuccess = provider.GetKeys().ToArray();

        var second = await provider.RefreshAsync(CancellationToken.None);
        var keysAfterFailure = provider.GetKeys().ToArray();

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(keysAfterSuccess.Length, keysAfterFailure.Length);
    }

    private static JwtSigningKeyProvider BuildProvider(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = new TestHttpClientFactory(httpClient);

        var authOptions = Options.Create(new AuthOptions
        {
            JwksUrl = "https://example.test/jwks.json",
            JwksRequestTimeoutSeconds = 5,
            JwksRefreshIntervalMinutes = 10,
            JwksRefreshFailureBackoffSeconds = 5,
            JwksRefreshMaxBackoffSeconds = 30
        });

        var telemetryOptions = Options.Create(new OpenTelemetryOptions
        {
            Enabled = false,
            EnableAuthKeyRefreshTracing = false
        });

        return new JwtSigningKeyProvider(
            factory,
            authOptions,
            telemetryOptions,
            NullLogger<JwtSigningKeyProvider>.Instance);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public TestHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private const string SampleJwksJson = """
{
  "keys": [
    {
      "kty": "oct",
      "kid": "test-key",
      "k": "AQAB"
    }
  ]
}
""";
}
