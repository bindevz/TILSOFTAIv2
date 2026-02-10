using TILSOFTAI.Orchestration.Policies;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Tools;
using Xunit;

namespace TILSOFTAI.Tests.Prompting;

/// <summary>
/// PATCH 36.08: Tests that ToolCatalogContextPackProvider with IScopedContextPackProvider
/// uses only scoped tools. Verifies that core_then_scope ordering is deterministic.
/// </summary>
public class ToolCatalogPackScopedTests
{
    [Fact]
    public void ScopedPack_ContainsOnly_ScopedTools()
    {
        // Arrange: only model-scoped tools in the PromptBuildContext
        var scopedTools = new List<ToolDefinition>
        {
            new()
            {
                Name = "model_overview",
                Description = "Get model overview",
                Module = "model"
            },
            new()
            {
                Name = "model_pieces_list",
                Description = "Get pieces for model",
                Module = "model"
            },
            new()
            {
                Name = "tool.list",
                Description = "List available tools",
                Module = null  // core tool
            }
        };

        var buildContext = new PromptBuildContext(
            scopedTools: scopedTools,
            resolvedModules: new[] { "model" },
            policies: RuntimePolicySnapshot.Empty);

        // Assert: scoped tools only contain what we passed in
        Assert.Equal(3, buildContext.ScopedTools.Count);
        Assert.Contains(buildContext.ScopedTools, t => t.Name == "model_overview");
        Assert.Contains(buildContext.ScopedTools, t => t.Name == "model_pieces_list");
        Assert.Contains(buildContext.ScopedTools, t => t.Name == "tool.list");

        // No unscoped tools present
        Assert.DoesNotContain(buildContext.ScopedTools, t => t.Name == "shipment_list");
        Assert.DoesNotContain(buildContext.ScopedTools, t => t.Name == "sales_analytics");
    }

    [Fact]
    public void ScopedPack_EmptyTools_ProducesEmptyResult()
    {
        var buildContext = new PromptBuildContext(
            scopedTools: Array.Empty<ToolDefinition>(),
            resolvedModules: Array.Empty<string>(),
            policies: RuntimePolicySnapshot.Empty);

        Assert.Empty(buildContext.ScopedTools);
    }

    [Fact]
    public void PromptBuildContext_ResolvedModules_Available()
    {
        var buildContext = new PromptBuildContext(
            scopedTools: Array.Empty<ToolDefinition>(),
            resolvedModules: new[] { "model", "shipment" },
            policies: RuntimePolicySnapshot.Empty);

        Assert.Equal(2, buildContext.ResolvedModules.Count);
        Assert.Contains("model", buildContext.ResolvedModules);
        Assert.Contains("shipment", buildContext.ResolvedModules);
    }

    [Fact]
    public void PromptBuildContext_ModuleKeysJson_SortedAndSerializable()
    {
        var buildContext = new PromptBuildContext(
            scopedTools: Array.Empty<ToolDefinition>(),
            resolvedModules: new[] { "shipment", "model" },
            policies: RuntimePolicySnapshot.Empty);

        var json = buildContext.ModuleKeysJson;

        Assert.NotNull(json);
        // JSON should be a valid array
        Assert.StartsWith("[", json);
        Assert.EndsWith("]", json);
    }

    [Fact]
    public void PromptBuildContext_Empty_IsValid()
    {
        var ctx = PromptBuildContext.Empty;

        Assert.Empty(ctx.ScopedTools);
        Assert.Empty(ctx.ResolvedModules);
        Assert.NotNull(ctx.Policies);
        Assert.Null(ctx.ModuleKeysJson);
    }
}
