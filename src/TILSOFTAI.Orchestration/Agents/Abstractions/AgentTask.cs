using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Supervisor;

namespace TILSOFTAI.Agents.Abstractions;

public sealed class AgentTask
{
    public string IntentType { get; set; } = string.Empty;
    public string? DomainHint { get; set; }
    public string Input { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string?> ContextPayload { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public bool RequiresWritePreparation { get; set; }
    /// <summary>
    /// When set by a domain agent, restricts the tool/module scope for this task.
    /// Sprint 2: not yet used by skeleton agents (Option 4), but available for future domain scoping.
    /// </summary>
    public IReadOnlyList<string>? AllowedModules { get; set; }

    /// <summary>
    /// Sprint 5: Structured capability request hint populated by SupervisorRuntime.
    /// Domain agents use this for structured capability resolution instead of parsing raw input.
    /// </summary>
    public CapabilityRequestHint? CapabilityHint { get; set; }

    public bool Stream { get; set; }
    public IProgress<SupervisorStreamEvent>? StreamObserver { get; set; }
    public bool AllowCache { get; set; } = true;
    public bool ContainsSensitive { get; set; }
    public IReadOnlyList<string>? SensitivityReasons { get; set; }
    public RequestPolicy? RequestPolicy { get; set; }
    public IReadOnlyList<ChatMessage>? MessageHistory { get; set; }
}
