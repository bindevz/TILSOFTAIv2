using System.Text.Json;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Agents.Domain;

/// <summary>
/// Sprint 4: Warehouse domain agent with native capability execution.
/// Resolves warehouse capabilities from ICapabilityRegistry and executes via ToolAdapterRegistry.
/// Falls back to LegacyChatPipelineBridge when no native capability matches.
/// </summary>
public sealed class WarehouseAgent : DomainAgentBase
{
    private readonly ICapabilityRegistry _capabilityRegistry;

    public WarehouseAgent(
        LegacyChatPipelineBridge bridge,
        ICapabilityRegistry capabilityRegistry,
        ILogger<WarehouseAgent> logger)
        : base(bridge, logger)
    {
        _capabilityRegistry = capabilityRegistry ?? throw new ArgumentNullException(nameof(capabilityRegistry));
    }

    public override string AgentId => "warehouse";
    public override string DisplayName => "Warehouse Domain Agent";
    public override IReadOnlyList<string> OwnedDomains { get; } = new[] { "warehouse" };

    public override async Task<AgentResult> ExecuteAsync(AgentTask task, AgentExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(context);

        Logger.LogInformation(
            "AgentExecution | AgentId: {AgentId} | IntentType: {IntentType} | DomainHint: {DomainHint}",
            AgentId, task.IntentType, task.DomainHint ?? "none");

        // Sprint 3: enforce write governance before any execution
        AgentWritePolicy.EnforceWriteGovernance(task, AgentId, Logger);

        // Sprint 4: attempt native capability resolution
        var capability = ResolveCapability(task.Input);
        if (capability is not null && context.ToolAdapterRegistry is not null)
        {
            Logger.LogInformation(
                "AgentNativePath | AgentId: {AgentId} | CapabilityKey: {CapabilityKey} | AdapterType: {AdapterType}",
                AgentId, capability.CapabilityKey, capability.AdapterType);

            var result = await ExecuteNativeAsync(capability, task, context, ct);

            Logger.LogInformation(
                "AgentExecutionCompleted | AgentId: {AgentId} | Path: native | Success: {Success}",
                AgentId, result.Success);

            return result;
        }

        // Fallback: delegate to legacy bridge when no native capability matches
        Logger.LogInformation(
            "AgentFallbackPath | AgentId: {AgentId} | Reason: {Reason}",
            AgentId, capability is null ? "no_capability_match" : "no_adapter_registry");

        var fallbackResult = await Bridge.ExecuteAsync(task, context, ct);

        Logger.LogInformation(
            "AgentExecutionCompleted | AgentId: {AgentId} | Path: bridge | Success: {Success}",
            AgentId, fallbackResult.Success);

        return fallbackResult;
    }

    /// <summary>
    /// Resolve a warehouse capability from the input text.
    /// Matches capability keys against normalized input keywords.
    /// </summary>
    internal CapabilityDescriptor? ResolveCapability(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var normalizedInput = input.ToLowerInvariant();
        var capabilities = _capabilityRegistry.GetByDomain("warehouse");

        // Match by capability key keywords present in input
        foreach (var cap in capabilities)
        {
            var keyParts = cap.CapabilityKey.Split('.', StringSplitOptions.RemoveEmptyEntries);

            // Check if input contains the significant domain+operation keywords
            // e.g. "warehouse.inventory.summary" → check for "inventory" + "summary"
            if (keyParts.Length >= 3)
            {
                var subject = keyParts[1]; // e.g. "inventory", "receipts"
                var action = keyParts[2];  // e.g. "summary", "by-item", "recent"

                if (normalizedInput.Contains(subject, StringComparison.OrdinalIgnoreCase)
                    && normalizedInput.Contains(action.Replace("-", " "), StringComparison.OrdinalIgnoreCase))
                {
                    return cap;
                }
            }
        }

        // Also allow direct capability key match (e.g. from a structured request)
        foreach (var cap in capabilities)
        {
            if (normalizedInput.Contains(cap.CapabilityKey, StringComparison.OrdinalIgnoreCase))
            {
                return cap;
            }
        }

        return null;
    }

    private async Task<AgentResult> ExecuteNativeAsync(
        CapabilityDescriptor capability,
        AgentTask task,
        AgentExecutionContext context,
        CancellationToken ct)
    {
        try
        {
            var adapter = context.ToolAdapterRegistry!.Resolve(capability.AdapterType);

            var metadata = new Dictionary<string, string?>(capability.IntegrationBinding!, StringComparer.OrdinalIgnoreCase);

            var request = new ToolExecutionRequest
            {
                TenantId = context.RuntimeContext.TenantId,
                AgentId = AgentId,
                SystemId = capability.TargetSystemId,
                CapabilityKey = capability.CapabilityKey,
                Operation = capability.Operation,
                ArgumentsJson = ExtractArgumentsJson(task),
                ExecutionMode = capability.ExecutionMode,
                CorrelationId = context.RuntimeContext.CorrelationId,
                Metadata = metadata!
            };

            var result = await adapter.ExecuteAsync(request, ct);

            if (!result.Success)
            {
                return AgentResult.Fail(
                    $"Capability execution failed: {result.ErrorCode}",
                    result.ErrorCode,
                    result.Detail);
            }

            return AgentResult.Ok(result.PayloadJson ?? string.Empty);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError(ex,
                "AgentNativePathError | AgentId: {AgentId} | CapabilityKey: {CapabilityKey}",
                AgentId, capability.CapabilityKey);

            return AgentResult.Fail($"Native execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Extract arguments JSON from the task context payload, or build a minimal payload
    /// from the task metadata if no structured arguments are available.
    /// </summary>
    private static string ExtractArgumentsJson(AgentTask task)
    {
        // If context payload contains an "arguments" key, use it
        if (task.ContextPayload.TryGetValue("arguments", out var argsValue)
            && !string.IsNullOrWhiteSpace(argsValue))
        {
            return argsValue;
        }

        // Build a minimal arguments payload from available context
        var args = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (task.ContextPayload.TryGetValue("tenantId", out var tid))
        {
            args["@TenantId"] = tid;
        }

        return args.Count > 0
            ? JsonSerializer.Serialize(args)
            : "{}";
    }
}
