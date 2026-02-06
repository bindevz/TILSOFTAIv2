using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Analytics;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Analytics;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.IntegrationTests.Infrastructure;
using Xunit;

namespace TILSOFTAI.IntegrationTests.Analytics;

/// <summary>
/// E2E integration tests for deep analytics workflow.
/// PATCH 29.09: Tests catalog→plan→validate→execute→render flow.
/// </summary>
/// <remarks>
/// These tests require a test database with seeded analytics catalog.
/// Mark as [Trait("Category", "Integration")] to exclude from unit test runs.
/// </remarks>
[Trait("Category", "Integration")]
public class DeepAnalyticsE2ETests
{
    /// <summary>
    /// Tests the complete analytics workflow for a Vietnamese query.
    /// Requires: Test database with model/collection catalog seeded.
    /// PATCH 31.04: Env-guarded test — runs when TEST_SQL_CONNECTION is set.
    /// </summary>
    [SqlServerAvailableFact]
    public async Task AnalyticsWorkflow_VietnameseQuery_ShouldReturnValidInsight()
    {
        // Arrange
        var query = "bao nhiêu model mùa 25/26";
        var detector = new AnalyticsIntentDetector();
        var intent = detector.Detect(query);
        var context = CreateTestContext();
        var orchestrator = CreateOrchestrator();

        // Act
        var result = await orchestrator.ExecuteAsync(query, intent, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("analytics workflow should complete successfully");
        result.Content.Should().NotBeNullOrWhiteSpace();
        
        // Content should start with headline (first line)
        var firstLine = result.Content!.Split('\n')[0];
        firstLine.Should().NotBeNullOrWhiteSpace("headline should be first line");
        firstLine.Should().NotContain("\r", "headline should not contain CR");
    }

    /// <summary>
    /// Tests intent detection triggers analytics workflow.
    /// </summary>
    [Fact]
    public void IntentDetector_AnalyticsQuery_ShouldDetectAsAnalytics()
    {
        // Arrange
        var detector = new AnalyticsIntentDetector();
        var queries = new[]
        {
            "bao nhiêu model mùa 25/26",
            "how many orders in 2025",
            "total products by category",
            "top 10 selling styles"
        };

        // Act & Assert
        foreach (var query in queries)
        {
            var result = detector.Detect(query);
            result.IsAnalytics.Should().BeTrue($"'{query}' should be detected as analytics");
            result.Confidence.Should().BeGreaterOrEqualTo(0.5m);
        }
    }

    /// <summary>
    /// Tests rendered output contains required elements.
    /// </summary>
    [Fact]
    public void InsightRenderer_CompleteInsight_ShouldRenderAllElements()
    {
        // Arrange
        var renderer = new InsightRenderer();
        var insight = new InsightOutput
        {
            Headline = new InsightHeadline { Text = "Total: 150 models for season 25/26" },
            Tables = new List<InsightTable>
            {
                new()
                {
                    Title = "Breakdown by Category",
                    Columns = new List<string> { "Category", "Count" },
                    Rows = new List<List<object?>>
                    {
                        new() { "Shirts", 50 },
                        new() { "Pants", 100 }
                    }
                }
            },
            Notes = new List<string>
            {
                "Filter: season = 25/26",
                "Limit: 200 rows",
                "Data as of: 2026-02-05 12:00 UTC"
            },
            Freshness = new DataFreshness { AsOfUtc = DateTime.UtcNow }
        };

        // Act
        var markdown = renderer.Render(insight);

        // Assert
        markdown.Should().StartWith("Total: 150 models", "headline should be first");
        markdown.Should().Contain("### Breakdown by Category", "table title should be included");
        markdown.Should().Contain("| Category | Count |", "table header should be included");
        markdown.Should().Contain("| Shirts | 50 |", "table data should be included");
        markdown.Should().Contain("**Notes:**", "notes section should be included");
        markdown.Should().Contain("Filter: season = 25/26", "filter note should be included");
        markdown.Should().Contain("Data as of: 2026-02-05", "freshness should be included");
    }

    /// <summary>
    /// Tests canonicalizer normalizes Vietnamese input correctly.
    /// </summary>
    [Fact]
    public void PromptCanonicalizer_VietnameseWithWhitespace_ShouldNormalize()
    {
        // Arrange
        var input = "  bao nhiêu\u00A0  model   mùa 25/26  \r\n"; // NBSP and CRLF

        // Act
        var result = TILSOFTAI.Orchestration.Normalization.PromptTextCanonicalizer.Canonicalize(input);

        // Assert
        result.Should().Be("bao nhiêu model mùa 25/26");
        result.Should().NotContain("\u00A0", "NBSP should be converted");
        result.Should().NotContain("\r", "CR should be removed");
    }

    #region Helper Methods

    private static TilsoftExecutionContext CreateTestContext()
    {
        return new TilsoftExecutionContext
        {
            TenantId = "test-tenant",
            UserId = "test-user",
            CorrelationId = Guid.NewGuid().ToString(),
            Language = "vi",
            Roles = new[] { "analytics.read" }
        };
    }

    private static AnalyticsOrchestrator CreateOrchestrator()
    {
        // Mock all dependencies - this is a structural test
        var toolCatalogResolver = Mock.Of<IToolCatalogResolver>();
        var toolHandler = Mock.Of<IToolHandler>();
        var toolGovernance = Mock.Of<ToolGovernance>();
        var llmClient = Mock.Of<ILlmClient>();
        var insightAssemblyService = Mock.Of<IInsightAssemblyService>();
        var renderer = new InsightRenderer();
        var persistence = Mock.Of<AnalyticsPersistence>();
        var cache = Mock.Of<AnalyticsCache>();
        var cacheWriteQueue = Mock.Of<ICacheWriteQueue>();
        var options = Options.Create(new AnalyticsOptions
        {
            MaxPlanRetries = 2,
            MaxToolCallsPerTurn = 10,
            EnableTaskFramePersistence = false,
            EnableInsightCache = false
        });
        var logger = Mock.Of<ILogger<AnalyticsOrchestrator>>();

        return new AnalyticsOrchestrator(
            toolCatalogResolver,
            toolHandler,
            toolGovernance,
            llmClient,
            insightAssemblyService,
            renderer,
            persistence,
            cache,
            cacheWriteQueue,
            options,
            logger);
    }

    #endregion
}
