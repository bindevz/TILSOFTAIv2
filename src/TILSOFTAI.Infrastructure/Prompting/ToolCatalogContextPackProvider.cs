using System.Text;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Infrastructure.Prompting;

/// <summary>
/// PATCH 36.02: Implements IScopedContextPackProvider — builds tool catalog pack
/// from scoped tools only (never global). Policy-driven via tool_catalog_context_pack.
/// </summary>
public sealed class ToolCatalogContextPackProvider : IScopedContextPackProvider
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

    /// <summary>
    /// Legacy IContextPackProvider — falls back to global tools (backward compat).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new Dictionary<string, string>();

        var tools = await _toolCatalogResolver.GetResolvedToolsAsync(cancellationToken);
        return BuildPack(tools, _options.Enabled, _options.MaxTools, _options.MaxTotalTokens,
            _options.MaxInstructionTokensPerTool, _options.MaxDescriptionTokensPerTool);
    }

    /// <summary>
    /// PATCH 36.02: Scoped provider — uses buildContext.ScopedTools and reads RuntimePolicy.
    /// </summary>
    public Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        PromptBuildContext buildContext,
        CancellationToken cancellationToken)
    {
        // Read effective limits from runtime policy (with system ceilings as fallback)
        var policies = buildContext.Policies;
        var enabled = policies.GetValueOrDefault("tool_catalog_context_pack", "enabled", _options.Enabled);
        var maxTools = policies.GetValueOrDefault("tool_catalog_context_pack", "maxTools", _options.MaxTools);
        var maxTokens = policies.GetValueOrDefault("tool_catalog_context_pack", "maxTotalTokens", _options.MaxTotalTokens);
        var maxInstructionTokens = policies.GetValueOrDefault("tool_catalog_context_pack", "maxInstructionTokensPerTool", _options.MaxInstructionTokensPerTool);
        var maxDescriptionTokens = policies.GetValueOrDefault("tool_catalog_context_pack", "maxDescriptionTokensPerTool", _options.MaxDescriptionTokensPerTool);

        // Use scoped tools — never global
        var tools = buildContext.ScopedTools;

        return Task.FromResult(BuildPack(tools, enabled, maxTools, maxTokens, maxInstructionTokens, maxDescriptionTokens));
    }

    private IReadOnlyDictionary<string, string> BuildPack(
        IReadOnlyList<ToolDefinition> tools,
        bool enabled,
        int maxTools,
        int maxTotalTokens,
        int maxInstructionTokens,
        int maxDescriptionTokens)
    {
        if (!enabled || tools.Count == 0)
            return new Dictionary<string, string>();

        var orderedTools = OrderTools(tools);
        var entries = BuildEntries(orderedTools, maxDescriptionTokens, maxInstructionTokens);

        if (entries.Count == 0)
            return new Dictionary<string, string>();

        TrimToToolCount(entries, maxTools);
        TrimToTokenBudget(entries, maxTotalTokens);

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

    private List<ToolEntry> BuildEntries(IEnumerable<ToolDefinition> tools, int maxDescTokens, int maxInstrTokens)
    {
        var entries = new List<ToolEntry>();

        foreach (var tool in tools)
        {
            var description = TrimText(tool.Description, maxDescTokens);
            var instruction = TrimText(tool.Instruction, maxInstrTokens);

            var text = BuildEntryText(tool.Name, description, instruction);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            entries.Add(new ToolEntry(tool, text, _budgeter.EstimateTokens(text)));
        }

        return entries;
    }

    private static void TrimToToolCount(List<ToolEntry> entries, int maxTools)
    {
        if (entries.Count <= maxTools)
            return;

        entries.RemoveRange(maxTools, entries.Count - maxTools);
    }

    private void TrimToTokenBudget(List<ToolEntry> entries, int maxTotalTokens)
    {
        var headerTokens = _budgeter.EstimateTokens("Available Tools:");
        var totalTokens = headerTokens + entries.Sum(entry => entry.Tokens);

        while (entries.Count > 0 && totalTokens > maxTotalTokens)
        {
            var lastIndex = entries.Count - 1;
            totalTokens -= entries[lastIndex].Tokens;
            entries.RemoveAt(lastIndex);
        }
    }

    private string TrimText(string? text, int maxTokens)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return _budgeter.TrimToTokens(text, maxTokens);
    }

    private static string BuildEntryText(string name, string description, string instruction)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var builder = new StringBuilder();
        builder.Append("- ").Append(name.Trim());

        if (!string.IsNullOrWhiteSpace(description))
            builder.Append(": ").Append(description.Trim());

        if (!string.IsNullOrWhiteSpace(instruction))
            builder.Append(" - ").Append(instruction.Trim());

        return builder.ToString();
    }

    /// <summary>
    /// PATCH 35: core_then_scope_order — core tools first, then by (Module, Name).
    /// </summary>
    private static IReadOnlyList<ToolDefinition> OrderTools(IReadOnlyList<ToolDefinition> tools)
    {
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
                coreTools.Add(tool);
            else
                scopeTools.Add(tool);
        }

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
