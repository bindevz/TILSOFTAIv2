using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Tools;
using Xunit;

namespace TILSOFTAI.Tests.Contract;

public sealed class PromptBudgetingTests
{
    [Fact]
    public async Task PromptBuilder_DoesNotIncludeJsonSchemaInSystemPrompt()
    {
        var chatOptions = Options.Create(new ChatOptions
        {
            MaxTokens = 4096
        });
        var builder = new PromptBuilder(
            chatOptions,
            new FakeContextPackProvider(),
            new TokenBudgetPolicy(),
            new ContextPackBudgeter());

        var tools = Enumerable.Range(0, 20)
            .Select(index => new ToolDefinition
            {
                Name = $"tool_{index}",
                Description = "desc",
                Instruction = "instruction",
                JsonSchema = "{\"type\":\"object\"}"
            })
            .ToList();

        var messages = new List<LlmMessage>
        {
            new("user", "hello")
        };

        var context = new TilsoftExecutionContext { Language = "en" };

        var request = await builder.BuildAsync(messages, tools, context, CancellationToken.None);

        Assert.DoesNotContain("JsonSchema:", request.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tool Definitions:", request.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Respond in language:", request.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContextPackBudgeter_DropsWholePacks()
    {
        var budgeter = new ContextPackBudgeter();
        var packs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["alpha"] = new string('a', 4000),
            ["beta"] = new string('b', 3500),
            ["gamma"] = new string('c', 100)
        };

        var result = budgeter.Budget(packs);

        Assert.Single(result);
        Assert.Equal("alpha", result[0].Key);
        Assert.Equal(packs["alpha"], result[0].Value);
    }

    private sealed class FakeContextPackProvider : IContextPackProvider
    {
        public Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(TilsoftExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }
    }
}
