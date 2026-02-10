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

    private readonly IToolCatalogResolver _toolCatalogResolver;
    private readonly ToolCatalogContextPackOptions _options;
    private readonly ContextPackBudgeter _budgeter;

    public ToolCatalogContextPackProvider(
        IToolCatalogResolver toolCatalogResolver,
        IOptions<ToolCatalogContextPackOptions> options,
        ContextPackBudgeter budgeter)
    {
        _toolCatalogResolver = toolCatalogResolver ?? throw new ArgumentNullException(nameof(toolCatalogResolver));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _budgeter = budgeter ?? throw new ArgumentNullException(nameof(budgeter));
    }

    public async Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // PATCH 29.04: When disabled, return empty to avoid tool instruction duplication
        if (!_options.Enabled)
        {
            return new Dictionary<string, string>();
        }

        var tools = await _toolCatalogResolver.GetResolvedToolsAsync(cancellationToken);
        if (tools.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var orderedTools = OrderTools(tools);
        var entries = BuildEntries(orderedTools);

        if (entries.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        TrimToToolCount(entries);
        TrimToTokenBudget(entries);

        var builder = new StringBuilder();
        builder.AppendLine("Available Tools:");

        foreach (var entry in entries)
        {
            builder.AppendLine();
            builder.Append(entry.Text);
        }

        return new Dictionary<string, string>
        {
            [ContextPackKey] = builder.ToString()
        };
    }

    private List<ToolEntry> BuildEntries(IEnumerable<ToolDefinition> tools)
    {
        var entries = new List<ToolEntry>();

        foreach (var tool in tools)
        {
            var description = TrimText(tool.Description, _options.MaxDescriptionTokensPerTool);
            var instruction = TrimText(tool.Instruction, _options.MaxInstructionTokensPerTool);

            var text = BuildEntryText(tool.Name, description, instruction);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            entries.Add(new ToolEntry(tool, text, _budgeter.EstimateTokens(text)));
        }

        return entries;
    }

    private void TrimToToolCount(List<ToolEntry> entries)
    {
        if (entries.Count <= _options.MaxTools)
        {
            return;
        }

        entries.RemoveRange(_options.MaxTools, entries.Count - _options.MaxTools);
    }

    private void TrimToTokenBudget(List<ToolEntry> entries)
    {
        var headerTokens = _budgeter.EstimateTokens("Available Tools:");
        var totalTokens = headerTokens + entries.Sum(entry => entry.Tokens);

        while (entries.Count > 0 && totalTokens > _options.MaxTotalTokens)
        {
            var lastIndex = entries.Count - 1;
            totalTokens -= entries[lastIndex].Tokens;
            entries.RemoveAt(lastIndex);
        }
    }

    private string TrimText(string? text, int maxTokens)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return _budgeter.TrimToTokens(text, maxTokens);
    }

    private static string BuildEntryText(string name, string description, string instruction)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("- ").Append(name.Trim());

        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.Append(": ").Append(description.Trim());
        }

        if (!string.IsNullOrWhiteSpace(instruction))
        {
            builder.Append(" - ").Append(instruction.Trim());
        }

        return builder.ToString();
    }

    /// <summary>
    /// PATCH 35: core_then_scope_order — core tools first, then by (Module, Name).
    /// PreferTools is deprecated; ordering is now deterministic by design.
    /// </summary>
    private IReadOnlyList<ToolDefinition> OrderTools(IReadOnlyList<ToolDefinition> tools)
    {
        // Core tools always surface first (stable order)
        var coreToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tool.list",
            "diagnostics_run",
            "action_request_write"
        };

        var coreTools = new List<ToolDefinition>();
        var scopeTools = new List<ToolDefinition>();

        foreach (var tool in tools)
        {
            if (coreToolNames.Contains(tool.Name))
            {
                coreTools.Add(tool);
            }
            else
            {
                scopeTools.Add(tool);
            }
        }

        // Sort scope tools by (Module, Name) for deterministic ordering
        scopeTools.Sort((a, b) =>
        {
            var moduleCompare = string.Compare(
                a.Module ?? string.Empty,
                b.Module ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
            return moduleCompare != 0
                ? moduleCompare
                : string.Compare(
                    a.Name ?? string.Empty,
                    b.Name ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
        });

        var ordered = new List<ToolDefinition>(coreTools.Count + scopeTools.Count);
        ordered.AddRange(coreTools);
        ordered.AddRange(scopeTools);

        return ordered;
    }

    private sealed record ToolEntry(ToolDefinition Tool, string Text, int Tokens);
}
