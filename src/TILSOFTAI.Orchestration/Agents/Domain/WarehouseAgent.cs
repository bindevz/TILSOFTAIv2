using System.Text.Json;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Agents.Domain;

/// <summary>
/// Sprint 5: Warehouse domain agent with native capability execution.
/// Resolves warehouse capabilities from ICapabilityRegistry using ICapabilityResolver
/// and executes via ToolAdapterRegistry.
/// Falls back to LegacyChatPipelineBridge when no native capability matches.
/// </summary>
public sealed class WarehouseAgent : DomainAgentBase
{
    private readonly ICapabilityRegistry _capabilityRegistry;
    private readonly ICapabilityResolver _capabilityResolver;

    public WarehouseAgent(
        LegacyChatPipelineBridge bridge,
        ICapabilityRegistry capabilityRegistry,
        ICapabilityResolver capabilityResolver,
        ILogger<WarehouseAgent> logger)
        : base(bridge, logger)
    {
        _capabilityRegistry = capabilityRegistry ?? throw new ArgumentNullException(nameof(capabilityRegistry));
        _capabilityResolver = capabilityResolver ?? throw new ArgumentNullException(nameof(capabilityResolver));
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

        // Sprint 5: attempt native capability resolution using structured resolver
        var candidates = _capabilityRegistry.GetByDomain("warehouse");
        var capability = ResolveCapability(task, candidates);

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
    /// Sprint 5: Resolve warehouse capability using structured resolver.
    /// Uses CapabilityHint from supervisor when available.
    /// </summary>
    internal CapabilityDescriptor? ResolveCapability(AgentTask task, IReadOnlyList<CapabilityDescriptor> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        // Use structured resolver with capability hint
        if (task.CapabilityHint is not null)
        {
            return _capabilityResolver.Resolve(task.CapabilityHint, candidates);
        }

        // Fallback: build a hint from raw input for backward compatibility
        var fallbackHint = new CapabilityRequestHint
        {
            Domain = "warehouse",
            SubjectKeywords = task.Input
                .Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLowerInvariant())
                .Distinct()
                .ToList()
        };

        return _capabilityResolver.Resolve(fallbackHint, candidates);
    }

    /// <summary>
    /// Sprint 4 backward-compat: Resolve a warehouse capability from the input text.
    /// </summary>
    [Obsolete("Sprint 5: use ResolveCapability(AgentTask, IReadOnlyList<CapabilityDescriptor>) with structured resolver instead")]
    internal CapabilityDescriptor? ResolveCapability(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var capabilities = _capabilityRegistry.GetByDomain("warehouse");

        // Check if input is an exact capability key
        foreach (var cap in capabilities)
        {
            if (string.Equals(cap.CapabilityKey, input.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return cap;
            }
        }

        // Fallback: build a hint and use the structured resolver
        var hint = new CapabilityRequestHint
        {
            Domain = "warehouse",
            SubjectKeywords = input
                .Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLowerInvariant())
                .Distinct()
                .ToList()
        };

        return _capabilityResolver.Resolve(hint, capabilities);
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
