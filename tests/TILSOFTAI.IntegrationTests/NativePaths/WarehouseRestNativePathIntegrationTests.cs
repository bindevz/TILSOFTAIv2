using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TILSOFTAI.Agents;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Agents.Domain;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Infrastructure.Http;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Supervisor.Classification;
using TILSOFTAI.Tools.Abstractions;
using Xunit;

namespace TILSOFTAI.IntegrationTests.NativePaths;

public sealed class WarehouseRestNativePathIntegrationTests
{
    private static LegacyChatPipelineBridge CreateUninitializedBridge() =>
        (LegacyChatPipelineBridge)RuntimeHelpers.GetUninitializedObject(typeof(LegacyChatPipelineBridge));

    [Fact]
    public async Task WarehouseExternalStockLookup_ShouldUseRestJsonAdapterThroughNativeCapabilityPath()
    {
        var handler = new CapturingHttpHandler();
        var restAdapter = new RestJsonToolAdapter(new HttpClient(handler));
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
                IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["baseUrl"] = "https://external-stock.local",
                    ["endpoint"] = "/warehouse/external-stock",
                    ["method"] = "GET",
                    ["retryCount"] = "1",
                    ["retryDelayMs"] = "0",
                    ["timeoutSeconds"] = "5"
                }
            }
        });
        var warehouseAgent = new WarehouseAgent(
            CreateUninitializedBridge(),
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
}
