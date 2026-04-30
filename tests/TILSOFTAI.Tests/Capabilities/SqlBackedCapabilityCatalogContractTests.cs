using FluentAssertions;
using TILSOFTAI.Orchestration.Answering;
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
        seed.Should().Contain("\"maxRowsForNarration\"");
        seed.Should().Contain("\"instructionsByLocale\"");
        seed.Should().Contain("\"hiddenColumns\"");
    }

    [Fact]
    public void SqlBackedCatalog_MapsSummaryPolicy()
    {
        var policy = ReadSeededAnswerPolicy("model.overview.by-code");

        policy.Summary.Mode.Should().Be(SummaryPolicy.ModeAi);
        policy.Summary.Style.Should().Be("business_concise");
        policy.Summary.MaxSentences.Should().Be(3);
        policy.Summary.IncludeFilters.Should().BeTrue();
        policy.Summary.IncludeRowCount.Should().BeTrue();
        policy.Summary.IncludeCaveats.Should().BeTrue();
        policy.Summary.InstructionsByLocale.Should().ContainKey("vi-VN");
        policy.Summary.InstructionsByLocale.Should().ContainKey("en-US");
        policy.Summary.ForbiddenClaims.Should().Contain("Do not expose hidden or masked fields.");
    }

    [Fact]
    public void SqlBackedCatalog_MapsTablePolicy()
    {
        var policy = ReadSeededAnswerPolicy("model.materials.by-code");

        policy.MaxRowsForChat.Should().Be(20);
        policy.MaxRowsForNarration.Should().Be(20);
        policy.Table.Enabled.Should().BeTrue();
        policy.Table.MaxDisplayedRows.Should().Be(20);
        policy.Table.IncludeRowCount.Should().BeTrue();
        policy.Table.IncludeTruncationNotice.Should().BeTrue();
        policy.NoData.IncludeUsedFilters.Should().BeTrue();
        policy.FollowUp.IncludeMissingFields.Should().BeTrue();
    }

    [Fact]
    public void SqlBackedCatalog_RejectsInvalidAnswerPolicy()
    {
        var act = () => AnswerPolicy.FromJson(
            """{"maxRowsForChat":20,"maxRowsForNarration":20,"summary":{"mode":"invent"},"table":{"enabled":true,"maxDisplayedRows":20},"noData":{},"followUp":{}}""");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*summary.mode*");
    }

    [Fact]
    public void SqlBackedCatalog_RejectsMissingAnswerPolicySection()
    {
        var act = () => AnswerPolicy.FromJson(
            """{"maxRowsForChat":20,"maxRowsForNarration":20,"summary":{"mode":"ai"},"table":{"enabled":true,"maxDisplayedRows":20},"noData":{}}""");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*followUp*");
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
        validation.Should().Contain("summary, table, noData, and followUp");
    }

    [Fact]
    public void CatalogValidation_InvalidPolicy_Fails()
    {
        var validation = ReadSql("997_validate_capability_catalog.sql");

        validation.Should().Contain("summary.mode must be one of ai, fallback, or disabled");
        validation.Should().Contain("maxRowsForChat must be greater than zero");
        validation.Should().Contain("maxRowsForNarration must be greater than zero");
        validation.Should().Contain("table.maxDisplayedRows must be greater than zero");
        validation.Should().Contain("summary.forbiddenClaims must be a JSON array");
        validation.Should().Contain("summary.instructionsByLocale must be a JSON object");
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

    private static AnswerPolicy ReadSeededAnswerPolicy(string capabilityKey)
    {
        var seed = ReadSql("008_seed_model_capability_catalog.sql");
        var policyMergeStart = seed.IndexOf("MERGE ai.CapabilityAnswerPolicy", StringComparison.Ordinal);
        policyMergeStart.Should().BeGreaterThanOrEqualTo(0);
        var prefix = $"(N'{capabilityKey}', N'";
        var start = seed.IndexOf(prefix, policyMergeStart, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        start += prefix.Length;
        var end = seed.IndexOf("')", start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start);
        return AnswerPolicy.FromJson(seed[start..end]);
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
