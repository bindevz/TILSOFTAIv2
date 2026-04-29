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

        snapshot.IsValid.Should().BeTrue();
        snapshot.CatalogFound.Should().BeTrue();
        snapshot.CatalogPath.Should().Be(path);
        snapshot.Version.Should().Be("test");
        snapshot.Capabilities.Should().ContainSingle(c => c.CapabilityKey == "warehouse.inventory.by-item");
        snapshot.ExternalConnections.Should().ContainKey("external-stock-api");
        snapshot.Capabilities.Single().ArgumentContract!.Arguments.Should().ContainSingle(rule => rule.Format == "item-number");
    }

    [Fact]
    public void FilePlatformCatalogProvider_ShouldExposeIntegrityErrors_WhenCatalogContractIsMissing()
    {
        var path = CreateCatalogFileWithoutContract();
        var provider = new FilePlatformCatalogProvider(
            Options.Create(new PlatformCatalogOptions { CatalogPath = path }),
            new Mock<ILogger<FilePlatformCatalogProvider>>().Object);

        var snapshot = provider.Load();

        snapshot.IsValid.Should().BeFalse();
        snapshot.IntegrityErrors.Should().Contain("capability_contract_required:warehouse.inventory.summary");
    }

    [Fact]
    public void FilePlatformCatalogProvider_ShouldRequireSchemaDialect_WhenSchemaRefIsConfigured()
    {
        var path = CreateCatalogFileWithSchemaRefOnly();
        var provider = new FilePlatformCatalogProvider(
            Options.Create(new PlatformCatalogOptions { CatalogPath = path }),
            new Mock<ILogger<FilePlatformCatalogProvider>>().Object);

        var snapshot = provider.Load();

        snapshot.IsValid.Should().BeFalse();
        snapshot.IntegrityErrors.Should().Contain("capability_contract_schema_dialect_required:warehouse.inventory.summary");
    }

    [Fact]
    public void ActivePlatformCatalog_ShouldExposeModelCodeContractsForModelTools()
    {
        var path = Path.Combine(FindRepositoryRoot(), "catalog", "platform-catalog.json");
        var provider = new FilePlatformCatalogProvider(
            Options.Create(new PlatformCatalogOptions { CatalogPath = path }),
            new Mock<ILogger<FilePlatformCatalogProvider>>().Object);

        var snapshot = provider.Load();
        var singleModelCapabilities = snapshot.Capabilities
            .Where(capability => capability.CapabilityKey is
                "model.overview.by-code"
                or "model.pieces.by-code"
                or "model.materials.by-code"
                or "model.packaging.by-code")
            .ToArray();

        snapshot.IsValid.Should().BeTrue();
        singleModelCapabilities.Should().HaveCount(4);
        foreach (var capability in singleModelCapabilities)
        {
            capability.ArgumentContract!.RequiredArguments.Should().Equal("modelCode");
            capability.ArgumentContract.AllowedArguments.Should().Equal("modelCode");
            capability.ArgumentContract.Arguments.Single().Name.Should().Be("modelCode");
            capability.ArgumentContract.Arguments.Single().Type.Should().Be("string");
        }

        var compare = snapshot.Capabilities.Single(capability => capability.CapabilityKey == "model.compare");
        compare.ArgumentContract!.RequiredArguments.Should().Equal("modelCodes");
        compare.ArgumentContract.AllowedArguments.Should().Equal("modelCodes");
        compare.ArgumentContract.Arguments.Single().Name.Should().Be("modelCodes");
        compare.ArgumentContract.Arguments.Single().Type.Should().Be("array");
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

    private static string CreateCatalogFileWithoutContract()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.platform-catalog.json");
        File.WriteAllText(path, """
        {
          "Version": "test",
          "ExternalConnections": {
            "Connections": {}
          },
          "Capabilities": [
            {
              "CapabilityKey": "warehouse.inventory.summary",
              "Domain": "warehouse",
              "AdapterType": "sql",
              "Operation": "execute_query",
              "TargetSystemId": "sql",
              "ExecutionMode": "readonly",
              "IntegrationBinding": {
                "storedProcedure": "dbo.ai_warehouse_inventory_summary"
              }
            }
          ]
        }
        """);
        return path;
    }

    private static string CreateCatalogFileWithSchemaRefOnly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.platform-catalog.json");
        File.WriteAllText(path, """
        {
          "Version": "test",
          "ExternalConnections": {
            "Connections": {}
          },
          "Capabilities": [
            {
              "CapabilityKey": "warehouse.inventory.summary",
              "Domain": "warehouse",
              "AdapterType": "sql",
              "Operation": "execute_query",
              "TargetSystemId": "sql",
              "ExecutionMode": "readonly",
              "IntegrationBinding": {
                "storedProcedure": "dbo.ai_warehouse_inventory_summary"
              },
              "ArgumentContract": {
                "ContractVersion": "1",
                "SchemaRef": "catalog-schema://warehouse.inventory.summary/1",
                "RequiredArguments": [],
                "AllowedArguments": [],
                "AllowAdditionalArguments": false,
                "Arguments": []
              }
            }
          ]
        }
        """);
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TILSOFTAI.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
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
