using TILSOFTAI.Domain.ExecutionContext;
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
    public bool Stream { get; set; }
    public IProgress<SupervisorStreamEvent>? StreamObserver { get; set; }
    public bool AllowCache { get; set; } = true;
    public bool ContainsSensitive { get; set; }
    public IReadOnlyList<string>? SensitivityReasons { get; set; }
    public RequestPolicy? RequestPolicy { get; set; }
    public IReadOnlyList<ChatMessage>? MessageHistory { get; set; }
}
