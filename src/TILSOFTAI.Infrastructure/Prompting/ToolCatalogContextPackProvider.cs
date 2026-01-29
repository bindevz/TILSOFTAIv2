using System.Text;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Infrastructure.Prompting;

public sealed class ToolCatalogContextPackProvider : IContextPackProvider
{
    private const string ContextPackKey = "tool_catalog";
    private const int MaxTools = 40;
    private const int MaxInstructionLength = 100;

    private readonly IToolCatalogResolver _toolCatalogResolver;
    private readonly LocalizationOptions _localizationOptions;

    public ToolCatalogContextPackProvider(
        IToolCatalogResolver toolCatalogResolver,
        IOptions<LocalizationOptions> localizationOptions)
    {
        _toolCatalogResolver = toolCatalogResolver ?? throw new ArgumentNullException(nameof(toolCatalogResolver));
        _localizationOptions = localizationOptions?.Value ?? throw new ArgumentNullException(nameof(localizationOptions));
    }

    public async Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var tools = await _toolCatalogResolver.GetResolvedToolsAsync(cancellationToken);
        if (tools.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        // Limit to MaxTools for token budget
        var limitedTools = tools.Take(MaxTools).ToList();

        var builder = new StringBuilder();
        builder.AppendLine("Available Tools:");

        foreach (var tool in limitedTools)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("- ").Append(tool.Name);

            if (!string.IsNullOrWhiteSpace(tool.Description))
            {
                builder.Append(": ").Append(tool.Description);
            }

            if (!string.IsNullOrWhiteSpace(tool.Instruction))
            {
                var instruction = tool.Instruction.Length > MaxInstructionLength
                    ? tool.Instruction.Substring(0, MaxInstructionLength) + "..."
                    : tool.Instruction;

                builder.Append(" - ").Append(instruction);
            }
        }

        return new Dictionary<string, string>
        {
            [ContextPackKey] = builder.ToString()
        };
    }
}
