using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public sealed partial class AgentClientFactory : IAgentClientFactory
{
    private readonly AgentRunOptionsFactory _optionsFactory;
    private readonly ILogger<AgentClientFactory> _logger;

    public AgentClientFactory(
        AgentRunOptionsFactory optionsFactory,
        ILogger<AgentClientFactory> logger)
    {
        _optionsFactory = optionsFactory ?? throw new ArgumentNullException(nameof(optionsFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IToolCallingAgent CreateToolCallingAgent(
        IReadOnlyList<AgentFunctionTool> tools,
        string instructions)
    {
        var options = _optionsFactory.Create();
        _logger.LogDebug(
            "AgentFrameworkClientCreated | ToolCount: {ToolCount} | MaxToolCallsPerTurn: {MaxToolCallsPerTurn}",
            tools.Count,
            options.MaxToolCallsPerTurn);

        return new CandidateGatedToolCallingAgent(tools, options);
    }

    private sealed partial class CandidateGatedToolCallingAgent : IToolCallingAgent
    {
        private readonly IReadOnlyList<AgentFunctionTool> _tools;
        private readonly AgentRunOptions _options;

        public CandidateGatedToolCallingAgent(IReadOnlyList<AgentFunctionTool> tools, AgentRunOptions options)
        {
            _tools = tools;
            _options = options;
        }

        public async Task<AgentRunResult> RunAsync(string message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var selected = _tools.FirstOrDefault();
            if (selected is null)
            {
                return new AgentRunResult
                {
                    ClarificationQuestion = "No ERP tool is available for this request."
                };
            }

            var arguments = new System.Text.Json.Nodes.JsonObject();
            var callbackArguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var missing = new List<string>();
            foreach (var argument in selected.Arguments.OrderBy(argument => argument.DisplayOrder))
            {
                var modelName = CapabilityFunctionNameMapper.MapParameter(argument.ArgumentName);
                var value = ExtractArgumentValue(message, argument);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (argument.IsRequired)
                    {
                        missing.Add(argument.ArgumentName);
                    }
                    continue;
                }

                arguments[modelName] = value;
                callbackArguments[modelName] = value;
            }

            if (missing.Count > 0)
            {
                return new AgentRunResult
                {
                    SelectedToolName = selected.Name,
                    SelectedCapabilityKey = selected.Capability.CapabilityKey,
                    ClarificationQuestion = $"Please provide {string.Join(", ", missing)}."
                };
            }

            var toolResult = await selected.InvokeAsync(callbackArguments, cancellationToken);

            return new AgentRunResult
            {
                SelectedToolName = selected.Name,
                SelectedCapabilityKey = selected.Capability.CapabilityKey,
                Arguments = arguments,
                ToolResult = toolResult
            };
        }

        private static string? ExtractArgumentValue(string message, CapabilityArgumentMetadata argument)
        {
            var name = argument.ArgumentName;
            if (name.Contains("date", StringComparison.OrdinalIgnoreCase))
            {
                return DateRegex().Match(message) is { Success: true } match ? match.Value : null;
            }

            if (name.Equals("currency", StringComparison.OrdinalIgnoreCase))
            {
                return CurrencyRegex().Match(message) is { Success: true } match ? match.Value.ToUpperInvariant() : null;
            }

            if (name.Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                return StatusRegex().Match(message) is { Success: true } match ? match.Value.ToLowerInvariant() : null;
            }

            if (name.Equals("season", StringComparison.OrdinalIgnoreCase))
            {
                return SeasonRegex().Match(message) is { Success: true } match ? match.Value : null;
            }

            if (name.Contains("code", StringComparison.OrdinalIgnoreCase)
                || name.Equals("item", StringComparison.OrdinalIgnoreCase))
            {
                return CodeRegex().Match(message) is { Success: true } match ? match.Value : null;
            }

            if (name.Equals("customer", StringComparison.OrdinalIgnoreCase)
                || name.Equals("supplier", StringComparison.OrdinalIgnoreCase)
                || name.Equals("warehouse", StringComparison.OrdinalIgnoreCase))
            {
                var quoted = QuotedRegex().Match(message);
                if (quoted.Success)
                {
                    return quoted.Groups["value"].Value;
                }

                return EntityAfterPrepositionRegex().Match(message) is { Success: true } match
                    ? match.Groups["value"].Value.Trim()
                    : null;
            }

            return null;
        }

        [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}/\d{1,2}/\d{2,4}\b|\b(?:last month|this month|tháng trước|hom nay|hôm nay)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex DateRegex();

        [GeneratedRegex(@"\b[A-Z]{3}\b", RegexOptions.CultureInvariant)]
        private static partial Regex CurrencyRegex();

        [GeneratedRegex(@"\b(open|closed|cancelled|all|mở|đóng|hủy)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex StatusRegex();

        [GeneratedRegex(@"\b(?:SS|FW)?\d{2,4}[A-Z]?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex SeasonRegex();

        [GeneratedRegex(@"\b(?:PO|SO|INV)-?\d+[A-Z0-9-]*\b|\b[A-Z]{2,}[A-Z0-9]+(?:-[A-Z0-9]+)+\b|\b[A-Z]\d{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex CodeRegex();

        [GeneratedRegex("[\"'“”](?<value>[^\"'“”]+)[\"'“”]", RegexOptions.CultureInvariant)]
        private static partial Regex QuotedRegex();

        [GeneratedRegex(@"\b(?:for|of|by|của|cho|ncc|supplier|customer|khách|khach)\s+(?<value>[A-Z0-9][\p{L}\p{N}\s.-]{1,80})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex EntityAfterPrepositionRegex();
    }
}
