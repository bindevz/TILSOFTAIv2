using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Infrastructure.Http;
using TILSOFTAI.Orchestration.Capabilities;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class PlatformCatalogTests
{
    [Fact]
    public void FilePlatformCatalogProvider_ShouldLoadCapabilitiesAndConnections()
    {
        var path = CreateCatalogFile();
        var provider = new FilePlatformCatalogProvider(
            Options.Create(new PlatformCatalogOptions { CatalogPath = path }),
            new Mock<ILogger<FilePlatformCatalogProvider>>().Object);

        var snapshot = provider.Load();

        snapshot.Capabilities.Should().ContainSingle(c => c.CapabilityKey == "warehouse.inventory.by-item");
        snapshot.ExternalConnections.Should().ContainKey("external-stock-api");
        snapshot.Capabilities.Single().ArgumentContract!.Arguments.Should().ContainSingle(rule => rule.Format == "item-number");
    }

    [Fact]
    public void CompositeCapabilityRegistry_ShouldLetPlatformCatalogOverrideBootstrapConfiguration()
    {
        var staticCapability = new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.inventory.summary",
            Domain = "warehouse",
            AdapterType = "sql",
            Operation = "execute_query",
            TargetSystemId = "sql",
            ExecutionMode = "readonly"
        };
        var bootstrapCapability = staticCapability.WithAdapter("rest-bootstrap");
        var platformCapability = staticCapability.WithAdapter("rest-platform");

        var registry = new CompositeCapabilityRegistry(
            new ICapabilitySource[]
            {
                new StaticCapabilitySource("static", new[] { staticCapability }),
                new StaticCapabilitySource("bootstrap-config", new[] { bootstrapCapability }),
                new StaticCapabilitySource("platform-catalog", new[] { platformCapability })
            },
            new Mock<ILogger<CompositeCapabilityRegistry>>().Object);

        registry.Resolve("warehouse.inventory.summary")!.AdapterType.Should().Be("rest-platform");
    }

    [Fact]
    public void CompositeExternalConnectionCatalog_ShouldPreferPlatformConnectionThenFallbackToBootstrap()
    {
        var platformProvider = new Mock<IPlatformCatalogProvider>();
        platformProvider.Setup(provider => provider.Load()).Returns(new PlatformCatalogSnapshot
        {
            ExternalConnections = new Dictionary<string, ExternalConnectionOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["external-stock-api"] = new() { BaseUrl = "https://platform.example" }
            }
        });

        var platform = new PlatformExternalConnectionCatalog(platformProvider.Object);
        var bootstrap = new ConfigurationExternalConnectionCatalog(Options.Create(new ExternalConnectionCatalogOptions
        {
            Connections = new Dictionary<string, ExternalConnectionOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["external-stock-api"] = new() { BaseUrl = "https://bootstrap.example" },
                ["bootstrap-only"] = new() { BaseUrl = "https://bootstrap-only.example" }
            }
        }));
        var composite = new CompositeExternalConnectionCatalog(
            platform,
            bootstrap,
            Options.Create(new PlatformCatalogOptions { AllowBootstrapConfigurationFallback = true }));

        composite.Resolve("external-stock-api")!.BaseUrl.Should().Be("https://platform.example");
        composite.Resolve("bootstrap-only")!.BaseUrl.Should().Be("https://bootstrap-only.example");
    }

    private static string CreateCatalogFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.platform-catalog.json");
        File.WriteAllText(path, """
        {
          "Version": "test",
          "ExternalConnections": {
            "Connections": {
              "external-stock-api": {
                "BaseUrl": "https://external-stock.test",
                "TimeoutSeconds": 5
              }
            }
          },
          "Capabilities": [
            {
              "CapabilityKey": "warehouse.inventory.by-item",
              "Domain": "warehouse",
              "AdapterType": "sql",
              "Operation": "execute_query",
              "TargetSystemId": "sql",
              "ExecutionMode": "readonly",
              "IntegrationBinding": {
                "storedProcedure": "dbo.ai_warehouse_inventory_by_item"
              },
              "ArgumentContract": {
                "RequiredArguments": [ "@ItemNo" ],
                "AllowedArguments": [ "@ItemNo" ],
                "AllowAdditionalArguments": false,
                "Arguments": [
                  {
                    "Name": "@ItemNo",
                    "Type": "string",
                    "Format": "item-number"
                  }
                ]
              }
            }
          ]
        }
        """);
        return path;
    }
}

file static class CapabilityDescriptorExtensions
{
    public static CapabilityDescriptor WithAdapter(this CapabilityDescriptor capability, string adapterType) => new()
    {
        CapabilityKey = capability.CapabilityKey,
        Domain = capability.Domain,
        AdapterType = adapterType,
        Operation = capability.Operation,
        TargetSystemId = capability.TargetSystemId,
        ExecutionMode = capability.ExecutionMode
    };
}
