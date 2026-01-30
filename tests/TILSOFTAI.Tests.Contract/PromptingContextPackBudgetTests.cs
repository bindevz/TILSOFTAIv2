using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Infrastructure.Prompting;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Tools;
using Xunit;

namespace TILSOFTAI.Tests.Contract;

public sealed class PromptingContextPackBudgetTests
{
    [Fact]
    public void TrimToTokens_RespectsCap()
    {
        var budgeter = new ContextPackBudgeter();
        var text = "one two three four";

        var trimmed = budgeter.TrimToTokens(text, 2);

        Assert.Equal("one two...", trimmed);
    }

    [Fact]
    public async Task Budget_RemovesTools_WhenOverMaxTotalTokens()
    {
        var tools = new List<ToolDefinition>
        {
            new() { Name = "beta", Module = "core", Description = "desc", Instruction = "instr" },
            new() { Name = "alpha", Module = "core", Description = "desc", Instruction = "instr" },
            new() { Name = "gamma", Module = "z", Description = "desc", Instruction = "instr" }
        };

        var options = Options.Create(new ToolCatalogContextPackOptions
        {
            MaxTools = 10,
            MaxTotalTokens = 12,
            MaxDescriptionTokensPerTool = 5,
            MaxInstructionTokensPerTool = 5,
            PreferTools = new[] { "beta" }
        });

        var provider = new ToolCatalogContextPackProvider(
            new FakeToolCatalogResolver(tools),
            options,
            new ContextPackBudgeter());

        var packs = await provider.GetContextPacksAsync(new TilsoftExecutionContext(), CancellationToken.None);
        var pack = packs["tool_catalog"];

        Assert.Contains("- beta", pack);
        Assert.Contains("- alpha", pack);
        Assert.DoesNotContain("- gamma", pack);
    }

    private sealed class FakeToolCatalogResolver : IToolCatalogResolver
    {
        private readonly IReadOnlyList<ToolDefinition> _tools;

        public FakeToolCatalogResolver(IReadOnlyList<ToolDefinition> tools)
        {
            _tools = tools;
        }

        public Task<IReadOnlyList<ToolDefinition>> GetResolvedToolsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tools);
        }
    }
}
