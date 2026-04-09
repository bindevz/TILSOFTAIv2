using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Conversations;

namespace TILSOFTAI.Supervisor;

public sealed class SupervisorRequest
{
    public string Input { get; set; } = string.Empty;
    public bool AllowCache { get; set; } = true;
    public bool ContainsSensitive { get; set; }
    public IReadOnlyList<string>? SensitivityReasons { get; set; }
    public RequestPolicy? RequestPolicy { get; set; }
    public IReadOnlyList<ChatMessage>? MessageHistory { get; set; }
    public string? IntentType { get; set; }
    public string? DomainHint { get; set; }
    public bool RequiresWritePreparation { get; set; }
    public bool Stream { get; set; }
    public IProgress<SupervisorStreamEvent>? StreamObserver { get; set; }
    public IReadOnlyDictionary<string, string?> Metadata { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}
