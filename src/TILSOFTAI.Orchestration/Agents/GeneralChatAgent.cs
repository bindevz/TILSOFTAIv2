using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Orchestration.Observability;

namespace TILSOFTAI.Agents;

/// <summary>
/// Supervisor-native general agent for unclassified chat and unsupported general requests.
/// </summary>
public sealed class GeneralChatAgent : IDomainAgent
{
    public const string AgentIdValue = "general-chat";
    public const string LegacyDomainHint = "legacy-chat";

    private readonly ILogger<GeneralChatAgent> _logger;
    private readonly RuntimeExecutionInstrumentation? _instrumentation;

    public GeneralChatAgent(
        ILogger<GeneralChatAgent> logger,
        RuntimeExecutionInstrumentation? instrumentation = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instrumentation = instrumentation;
    }

    public string AgentId => AgentIdValue;

    public string DisplayName => "General Chat Agent";

    public IReadOnlyList<string> OwnedDomains { get; } = new[]
    {
        AgentIdValue,
        LegacyDomainHint,
        "cross-domain"
    };

    public bool CanHandle(AgentTask task)
    {
        if (task is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(task.DomainHint))
        {
            return true;
        }

        return OwnedDomains.Any(domain =>
            string.Equals(domain, task.DomainHint, StringComparison.OrdinalIgnoreCase));
    }

    public Task<AgentResult> ExecuteAsync(AgentTask task, AgentExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(context);

        if (ShouldUseLegacyFallback(task))
        {
            return Task.FromResult(ExecuteRetiredLegacyFallback(task));
        }

        if (IsGeneralConversation(task.Input))
        {
            const string response = "I can help with warehouse and accounting questions. For operational data, include the domain or capability you want to use.";
            _logger.LogInformation(
                "GeneralAgentNativeResponse | AgentId: {AgentId} | DomainHint: {DomainHint}",
                AgentId,
                task.DomainHint ?? "none");
            return Task.FromResult(AgentResult.Ok(response));
        }

        _logger.LogInformation(
            "GeneralAgentUnsupported | AgentId: {AgentId} | Reason: {Reason} | DomainHint: {DomainHint}",
            AgentId,
            BridgeFallbackReasons.UnsupportedGeneralRequest,
            task.DomainHint ?? "none");

        return Task.FromResult(AgentResult.Fail(
            "No native domain capability matched this request. Provide a warehouse/accounting domain or an explicit capability key.",
            "GENERAL_REQUEST_UNSUPPORTED",
            new
            {
                reason = BridgeFallbackReasons.UnsupportedGeneralRequest,
                domainHint = task.DomainHint
            }));
    }

    private AgentResult ExecuteRetiredLegacyFallback(AgentTask task)
    {
        var sw = Stopwatch.StartNew();
        sw.Stop();

        _instrumentation?.RecordBridgeFallback(
            AgentId,
            BridgeFallbackReasons.ExplicitLegacyFallback,
            sw.Elapsed,
            success: false);

        _logger.LogInformation(
            "GeneralAgentLegacyFallbackRetired | AgentId: {AgentId} | Reason: {Reason}",
            AgentId,
            BridgeFallbackReasons.ExplicitLegacyFallback);

        return AgentResult.Fail(
            "Legacy chat fallback has been retired. Use supervisor-native warehouse/accounting capabilities or a supported general request.",
            "LEGACY_RUNTIME_RETIRED",
            new
            {
                reason = BridgeFallbackReasons.ExplicitLegacyFallback,
                domainHint = task.DomainHint
            });
    }

    private static bool ShouldUseLegacyFallback(AgentTask task)
    {
        if (string.Equals(task.DomainHint, LegacyDomainHint, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return task.ContextPayload.TryGetValue("legacyFallback", out var legacyFallback)
            && bool.TryParse(legacyFallback, out var enabled)
            && enabled;
    }

    private static bool IsGeneralConversation(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Trim().ToLowerInvariant();
        var generalPrefixes = new[]
        {
            "hello",
            "hi",
            "hey",
            "help",
            "what can you do",
            "how are you"
        };

        return generalPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
