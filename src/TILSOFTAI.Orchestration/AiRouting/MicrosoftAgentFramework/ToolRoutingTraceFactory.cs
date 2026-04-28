using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public static class ToolRoutingTraceFactory
{
    public static ToolRoutingTrace FromSuccess(
        AgentToolRoutingRequest request,
        HardSignalSet hardSignals,
        CapabilityRetrievalResult retrieval,
        IReadOnlyList<AgentFunctionTool> tools,
        AgentRunResult agentResult,
        AssistantAnswer answer,
        IReadOnlyDictionary<string, double> stageLatencyMs,
        long startedAt) => new()
    {
        CorrelationId = ParseCorrelationId(request.ExecutionContext.CorrelationId),
        TenantId = SafeRequired(request.ExecutionContext.TenantId),
        UserId = SafeRequired(request.ExecutionContext.UserId),
        Locale = request.Locale,
        UserMessageHash = Hash(request.Message),
        UserMessageRedacted = Redact(request.Message),
        CandidateDomainsJson = JsonSerializer.Serialize(retrieval.Domains),
        CandidateToolsJson = JsonSerializer.Serialize(retrieval.Capabilities.Select(candidate => new
        {
            candidate.Metadata.CapabilityKey,
            candidate.Metadata.Domain,
            candidate.Score
        })),
        HardSignalsJson = JsonSerializer.Serialize(hardSignals),
        AdvertisedFunctionToolsJson = JsonSerializer.Serialize(tools.Select(tool => new
        {
            tool.Name,
            tool.Capability.CapabilityKey,
            tool.Capability.Domain,
            tool.Capability.ExecutionMode
        })),
        SelectedTool = agentResult.SelectedCapabilityKey,
        SelectedFunction = agentResult.SelectedToolName,
        CandidateDomainCount = retrieval.Domains.Count,
        CandidateCapabilityCount = retrieval.Capabilities.Count,
        AdvertisedToolCount = tools.Count,
        ArgumentsJson = agentResult.Arguments.ToJsonString(),
        ArgumentsBeforeNormalizationJson = agentResult.Arguments.ToJsonString(),
        ArgumentsAfterNormalizationJson = JsonSerializer.Serialize(agentResult.ToolResult?.Arguments),
        ValidationResultJson = JsonSerializer.Serialize(new
        {
            routed = true,
            clarification = !string.IsNullOrWhiteSpace(agentResult.ClarificationQuestion),
            toolResultStatus = agentResult.ToolResult?.Status,
            missing = agentResult.ToolResult?.MissingArguments,
            invalid = agentResult.ToolResult?.InvalidArguments
        }),
        AdapterType = agentResult.ToolResult?.ExecutionMetadata.AdapterType,
        RowCount = agentResult.ToolResult?.RowCount ?? answer.Provenance.RowCount,
        AnswerMode = request.RequestedAnswerMode.ToString(),
        LatencyMs = ElapsedMs(startedAt),
        LatencyByStageJson = JsonSerializer.Serialize(stageLatencyMs),
        ModelProvider = "microsoft-agent-framework",
        Success = true
    };

    public static ToolRoutingTrace FromNotHandled(
        AgentToolRoutingRequest request,
        HardSignalSet? hardSignals,
        CapabilityRetrievalResult? retrieval,
        IReadOnlyList<AgentFunctionTool>? tools,
        string reason,
        IReadOnlyDictionary<string, double> stageLatencyMs,
        long startedAt) => new()
    {
        CorrelationId = ParseCorrelationId(request.ExecutionContext.CorrelationId),
        TenantId = SafeRequired(request.ExecutionContext.TenantId),
        UserId = SafeRequired(request.ExecutionContext.UserId),
        Locale = request.Locale,
        UserMessageHash = Hash(request.Message),
        UserMessageRedacted = Redact(request.Message),
        CandidateDomainsJson = retrieval is null ? null : JsonSerializer.Serialize(retrieval.Domains),
        CandidateToolsJson = retrieval is null ? null : JsonSerializer.Serialize(retrieval.Capabilities.Select(candidate => new
        {
            candidate.Metadata.CapabilityKey,
            candidate.Metadata.Domain,
            candidate.Score
        })),
        HardSignalsJson = hardSignals is null ? null : JsonSerializer.Serialize(hardSignals),
        AdvertisedFunctionToolsJson = tools is null ? null : JsonSerializer.Serialize(tools.Select(tool => new
        {
            tool.Name,
            tool.Capability.CapabilityKey,
            tool.Capability.Domain,
            tool.Capability.ExecutionMode
        })),
        CandidateDomainCount = retrieval?.Domains.Count,
        CandidateCapabilityCount = retrieval?.Capabilities.Count,
        AdvertisedToolCount = tools?.Count,
        ValidationResultJson = JsonSerializer.Serialize(new
        {
            routed = false,
            reason
        }),
        AnswerMode = request.RequestedAnswerMode.ToString(),
        LatencyMs = ElapsedMs(startedAt),
        LatencyByStageJson = JsonSerializer.Serialize(stageLatencyMs),
        ModelProvider = "microsoft-agent-framework",
        Success = false,
        ErrorCode = reason
    };

    public static ToolRoutingTrace FromFailure(
        AgentToolRoutingRequest request,
        Exception exception,
        long startedAt) => new()
    {
        CorrelationId = ParseCorrelationId(request.ExecutionContext.CorrelationId),
        TenantId = SafeRequired(request.ExecutionContext.TenantId),
        UserId = SafeRequired(request.ExecutionContext.UserId),
        Locale = request.Locale,
        UserMessageHash = Hash(request.Message),
        UserMessageRedacted = Redact(request.Message),
        AnswerMode = request.RequestedAnswerMode.ToString(),
        LatencyMs = ElapsedMs(startedAt),
        ModelProvider = "microsoft-agent-framework",
        Success = false,
        ErrorCode = exception.GetType().Name
    };

    private static int ElapsedMs(long startedAt) =>
        (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

    private static Guid ParseCorrelationId(string? correlationId) =>
        Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();

    private static string SafeRequired(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private static byte[] Hash(string value) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));

    private static string Redact(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Length <= 512 ? value : value[..512];
}
