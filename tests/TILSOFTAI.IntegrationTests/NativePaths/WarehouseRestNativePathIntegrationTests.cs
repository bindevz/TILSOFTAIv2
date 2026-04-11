using System.Net;
using System.Text.Json;
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

public sealed class WarehouseRestNativePathIntegrationTests
{
    [Fact]
    public async Task WarehouseExternalStockLookup_ShouldUseRestJsonAdapterThroughNativeCapabilityPath()
    {
        var handler = new CapturingHttpHandler();
        var catalog = new ConfigurationExternalConnectionCatalog(Options.Create(new ExternalConnectionCatalogOptions
        {
            Connections = new Dictionary<string, ExternalConnectionOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["external-stock-api"] = new()
                {
                    BaseUrl = "https://external-stock.local",
                    AuthScheme = "Bearer",
                    AuthTokenSecret = "tilsoft/external-stock/token",
                    RetryCount = 1,
                    RetryDelayMs = 0,
                    TimeoutSeconds = 5
                }
            }
        }));
        var restAdapter = new RestJsonToolAdapter(
            new HttpClient(handler),
            catalog,
            new FakeSecretProvider(new Dictionary<string, string>
            {
                ["tilsoft/external-stock/token"] = "stock-token"
            }));
        var adapterRegistry = new ToolAdapterRegistry(new IToolAdapter[] { restAdapter });

        var capabilityResolver = new StructuredCapabilityResolver(
            new Mock<ILogger<StructuredCapabilityResolver>>().Object);
        var capabilityRegistry = new InMemoryCapabilityRegistry(new[]
        {
            new CapabilityDescriptor
            {
                CapabilityKey = "warehouse.external-stock.lookup",
                Domain = "warehouse",
                AdapterType = RestJsonToolAdapter.Type,
                Operation = ToolAdapterOperationNames.ExecuteHttpJson,
                TargetSystemId = "external-stock-api",
                RequiredRoles = new[] { "warehouse_external_read" },
                ArgumentContract = new CapabilityArgumentContract
                {
                    RequiredArguments = new[] { "@ItemNo" },
                    AllowedArguments = new[] { "@ItemNo" },
                    AllowAdditionalArguments = false
                },
                IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["connectionName"] = "external-stock-api",
                    ["endpoint"] = "/warehouse/external-stock",
                    ["method"] = "GET"
                }
            }
        });
        var warehouseAgent = new WarehouseAgent(
            capabilityRegistry,
            capabilityResolver,
            new Mock<ILogger<WarehouseAgent>>().Object);
        var agentRegistry = new DomainAgentRegistry(
            new IDomainAgent[] { warehouseAgent },
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
                Input = "warehouse external stock lookup",
                DomainHint = "warehouse",
                Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["capabilityKey"] = "warehouse.external-stock.lookup",
                    ["arguments"] = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["@ItemNo"] = "CHAIR-001"
                    })
                }
            },
            new TilsoftExecutionContext
            {
                TenantId = "tenant-rest",
                UserId = "user-rest",
                CorrelationId = "corr-rest",
                Roles = new[] { "warehouse_external_read" }
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SelectedAgentId.Should().Be("warehouse");
        result.Output.Should().Contain("CHAIR-001");
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Contain("/warehouse/external-stock");
        handler.LastRequest!.RequestUri!.Query.Should().Contain("ItemNo=CHAIR-001");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("stock-token");
        handler.LastRequest.Headers.GetValues("X-TILSOFTAI-Tenant").Single().Should().Be("tenant-rest");
    }

    private sealed class CapturingHttpHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"itemNo\":\"CHAIR-001\",\"available\":12}")
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
