using System.Net;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Agents;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Agents.Domain;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Secrets;
using TILSOFTAI.Infrastructure.Http;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Supervisor.Classification;
using TILSOFTAI.Tools.Abstractions;
using Xunit;

namespace TILSOFTAI.IntegrationTests.NativePaths;

public sealed class AccountingRestNativePathIntegrationTests
{
    private static LegacyChatPipelineBridge CreateUninitializedBridge() =>
        (LegacyChatPipelineBridge)RuntimeHelpers.GetUninitializedObject(typeof(LegacyChatPipelineBridge));

    [Fact]
    public async Task AccountingExchangeRateLookup_ShouldUseSecretBackedConnectionCatalog()
    {
        var handler = new CapturingHttpHandler();
        var catalog = new ConfigurationExternalConnectionCatalog(Options.Create(new ExternalConnectionCatalogOptions
        {
            Connections = new Dictionary<string, ExternalConnectionOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["fx-rate-api"] = new()
                {
                    BaseUrl = "https://fx-rates.local",
                    ApiKeyHeader = "X-Api-Key",
                    ApiKeySecret = "tilsoft/fx/api-key",
                    RetryCount = 1,
                    RetryDelayMs = 0,
                    TimeoutSeconds = 5
                }
            }
        }));
        var secretProvider = new FakeSecretProvider(new Dictionary<string, string>
        {
            ["tilsoft/fx/api-key"] = "fx-key"
        });
        var restAdapter = new RestJsonToolAdapter(new HttpClient(handler), catalog, secretProvider);
        var adapterRegistry = new ToolAdapterRegistry(new IToolAdapter[] { restAdapter });
        var capabilityResolver = new StructuredCapabilityResolver(
            new Mock<ILogger<StructuredCapabilityResolver>>().Object);
        var capabilityRegistry = new InMemoryCapabilityRegistry(new[]
        {
            new CapabilityDescriptor
            {
                CapabilityKey = "accounting.exchange-rate.lookup",
                Domain = "accounting",
                AdapterType = RestJsonToolAdapter.Type,
                Operation = ToolAdapterOperationNames.ExecuteHttpJson,
                TargetSystemId = "fx-rate-api",
                RequiredRoles = new[] { "accounting_external_read" },
                ArgumentContract = new CapabilityArgumentContract
                {
                    RequiredArguments = new[] { "@CurrencyCode" },
                    AllowedArguments = new[] { "@CurrencyCode" },
                    AllowAdditionalArguments = false
                },
                IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["connectionName"] = "fx-rate-api",
                    ["endpoint"] = "/rates/latest",
                    ["method"] = "GET"
                }
            }
        });
        var accountingAgent = new AccountingAgent(
            CreateUninitializedBridge(),
            capabilityRegistry,
            capabilityResolver,
            new Mock<ILogger<AccountingAgent>>().Object);
        var agentRegistry = new DomainAgentRegistry(
            new IDomainAgent[] { accountingAgent },
            new Mock<ILogger<DomainAgentRegistry>>().Object);
        var runtime = new SupervisorRuntime(
            new KeywordIntentClassifier(new Mock<ILogger<KeywordIntentClassifier>>().Object),
            agentRegistry,
            new Mock<IApprovalEngine>().Object,
            adapterRegistry,
            new Mock<ILogger<SupervisorRuntime>>().Object);

        var result = await runtime.RunAsync(
            new SupervisorRequest
            {
                Input = "accounting exchange rate",
                DomainHint = "accounting",
                Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["capabilityKey"] = "accounting.exchange-rate.lookup",
                    ["arguments"] = "{\"@CurrencyCode\":\"USD\"}"
                }
            },
            new TilsoftExecutionContext
            {
                TenantId = "tenant-fx",
                UserId = "user-fx",
                CorrelationId = "corr-fx",
                Roles = new[] { "accounting_external_read" }
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SelectedAgentId.Should().Be("accounting");
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Contain("/rates/latest");
        handler.LastRequest.RequestUri.Query.Should().Contain("CurrencyCode=USD");
        handler.LastRequest.Headers.GetValues("X-Api-Key").Single().Should().Be("fx-key");
        handler.LastRequest.Headers.GetValues("X-TILSOFTAI-Tenant").Single().Should().Be("tenant-fx");
    }

    private sealed class CapturingHttpHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"currency\":\"USD\",\"rate\":1.0}")
            });
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
