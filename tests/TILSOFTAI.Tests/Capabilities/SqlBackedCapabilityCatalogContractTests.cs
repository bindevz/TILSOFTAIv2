using FluentAssertions;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class SqlBackedCapabilityCatalogContractTests
{
    private static readonly string[] ExpectedCapabilityKeys =
    [
        "model.count",
        "model.overview.by-code",
        "model.pieces.by-code",
        "model.materials.by-code",
        "model.compare",
        "model.packaging.by-code"
    ];

    [Fact]
    public void SqlBackedCatalog_SeedsExactlySixModelCapabilities()
    {
        var seed = ReadSql("008_seed_model_capability_catalog.sql");

        foreach (var capabilityKey in ExpectedCapabilityKeys)
        {
            seed.Should().Contain($"N'{capabilityKey}'");
        }

        seed.Should().Contain("CapabilityKey, Domain, FunctionName");
        seed.Should().Contain("Domain = N'model'");
    }

    [Fact]
    public void SqlBackedCatalog_UsesModelFacingArgumentNamesOnly()
    {
        var seed = ReadSql("008_seed_model_capability_catalog.sql");

        seed.Should().Contain("N'modelCode'");
        seed.Should().Contain("N'modelCodes'");
        seed.Should().Contain("N'$.modelCode'");
        seed.Should().Contain("N'$.modelCodes'");
        seed.Should().NotContain("N'modelId'");
        seed.Should().NotContain("N'model_id'");
        seed.Should().NotContain("N'$.modelId'");
        seed.Should().NotContain("N'$.model_id'");
    }

    [Fact]
    public void SqlBackedCatalog_StoresResultSchemasAndAnswerPoliciesInSql()
    {
        var seed = ReadSql("008_seed_model_capability_catalog.sql");

        seed.Should().Contain("MERGE ai.CapabilityResultSchema");
        seed.Should().Contain("MERGE ai.CapabilityAnswerPolicy");
        seed.Should().Contain("MERGE ai.CapabilitySensitivityPolicy");
        seed.Should().Contain("\"columns\"");
        seed.Should().Contain("\"maxRowsForChat\"");
        seed.Should().Contain("\"hiddenColumns\"");
    }

    [Fact]
    public void SqlBackedCatalog_ValidatorRejectsCatalogDrift()
    {
        var validation = ReadSql("997_validate_capability_catalog.sql");

        validation.Should().Contain("Expected exactly 6 enabled model capabilities");
        validation.Should().Contain("Enabled non-model capabilities are not allowed");
        validation.Should().Contain("modelId");
        validation.Should().Contain("model_id");
        validation.Should().Contain("reference missing stored procedures");
        validation.Should().Contain("valid result schema");
    }

    [Fact]
    public void Runtime_ModelE2E_UsesSqlBackedCatalog()
    {
        var repositoryRoot = FindRepositoryRoot();
        var registrations = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Api", "Extensions", "AddTilsoftAiSqlExtensions.cs"));
        var orchestrationRegistrations = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "OrchestrationServiceCollectionExtensions.cs"));

        registrations.Should().Contain("SqlCapabilityCatalogRepository");
        registrations.Should().Contain("ICapabilityRegistry");
        registrations.Should().Contain("ISqlBackedCapabilityCatalog");
        registrations.Should().Contain("ICapabilityCatalogReloader");
        orchestrationRegistrations.Should().NotContain("ModelCapabilities.All");
        orchestrationRegistrations.Should().NotContain("new InMemoryCapabilityRegistry");
    }

    private static string ReadSql(string fileName)
    {
        var repositoryRoot = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(repositoryRoot, "sql", "current", fileName));
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

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
