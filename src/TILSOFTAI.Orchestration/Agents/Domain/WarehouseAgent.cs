using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Agents.Domain;

/// <summary>
/// Sprint 5: Warehouse domain agent with native capability execution.
/// Resolves warehouse capabilities from ICapabilityRegistry using ICapabilityResolver
/// and executes via ToolAdapterRegistry.
/// </summary>
public sealed class WarehouseAgent : DomainAgentBase
{
    private readonly ICapabilityRegistry _capabilityRegistry;
    private readonly ICapabilityResolver _capabilityResolver;
    private readonly RuntimeExecutionInstrumentation? _instrumentation;

    public WarehouseAgent(
        ICapabilityRegistry capabilityRegistry,
        ICapabilityResolver capabilityResolver,
        ILogger<WarehouseAgent> logger,
        RuntimeExecutionInstrumentation? instrumentation = null)
        : base(logger)
    {
        _capabilityRegistry = capabilityRegistry ?? throw new ArgumentNullException(nameof(capabilityRegistry));
        _capabilityResolver = capabilityResolver ?? throw new ArgumentNullException(nameof(capabilityResolver));
        _instrumentation = instrumentation;
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

        if (capability is not null)
        {
            Logger.LogInformation(
                "AgentNativePath | AgentId: {AgentId} | CapabilityKey: {CapabilityKey} | AdapterType: {AdapterType}",
                AgentId, capability.CapabilityKey, capability.AdapterType);

            if (context.ToolAdapterRegistry is null)
            {
                _instrumentation?.RecordAdapterFailure(
                    AgentId,
                    capability.CapabilityKey,
                    capability.AdapterType,
                    "TOOL_ADAPTER_REGISTRY_UNAVAILABLE");

                return AgentResult.Fail(
                    "Tool adapter registry is unavailable for native capability execution.",
                    "TOOL_ADAPTER_REGISTRY_UNAVAILABLE",
                    new { capabilityKey = capability.CapabilityKey, adapterType = capability.AdapterType });
            }

            var result = await ExecuteNativeAsync(capability, task, context, ct);

            Logger.LogInformation(
                "AgentExecutionCompleted | AgentId: {AgentId} | Path: native | Success: {Success}",
                AgentId, result.Success);

            return result;
        }

        Logger.LogInformation(
            "AgentUnsupportedPath | AgentId: {AgentId} | Reason: {Reason}",
            AgentId, BridgeFallbackReasons.NoCapabilityMatch);

        return AgentResult.Fail(
            "No native warehouse capability matched this request.",
            "DOMAIN_CAPABILITY_NOT_FOUND",
            new { domain = "warehouse", reason = BridgeFallbackReasons.NoCapabilityMatch });
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
        var sw = Stopwatch.StartNew();
        var success = false;
        var adapterType = capability.AdapterType;

        try
        {
            var policy = CapabilityAccessPolicy.Evaluate(capability, context.RuntimeContext);
            if (!policy.Allowed)
            {
                _instrumentation?.RecordAdapterFailure(
                    AgentId,
                    capability.CapabilityKey,
                    adapterType,
                    policy.Code);

                return AgentResult.Fail(
                    "Capability access denied.",
                    policy.Code,
                    policy.Detail);
            }

            var argumentsJson = ExtractArgumentsJson(task);
            var validation = CapabilityArgumentValidator.Validate(capability, argumentsJson);
            if (!validation.IsValid)
            {
                _instrumentation?.RecordAdapterFailure(
                    AgentId,
                    capability.CapabilityKey,
                    adapterType,
                    CapabilityArgumentValidator.ValidationFailedCode);

                return AgentResult.Fail(
                    "Capability argument validation failed.",
                    CapabilityArgumentValidator.ValidationFailedCode,
                    validation.Detail);
            }

            var adapter = context.ToolAdapterRegistry!.Resolve(capability.AdapterType);
            adapterType = adapter.AdapterType;

            var metadata = new Dictionary<string, string?>(capability.IntegrationBinding!, StringComparer.OrdinalIgnoreCase);

            var request = new ToolExecutionRequest
            {
                TenantId = context.RuntimeContext.TenantId,
                AgentId = AgentId,
                SystemId = capability.TargetSystemId,
                CapabilityKey = capability.CapabilityKey,
                Operation = capability.Operation,
                ArgumentsJson = argumentsJson,
                ExecutionMode = capability.ExecutionMode,
                CorrelationId = context.RuntimeContext.CorrelationId,
                Metadata = metadata!
            };

            var result = await adapter.ExecuteAsync(request, ct);

            if (!result.Success)
            {
                _instrumentation?.RecordAdapterFailure(
                    AgentId,
                    capability.CapabilityKey,
                    adapterType,
                    result.ErrorCode);
                return AgentResult.Fail(
                    $"Capability execution failed: {result.ErrorCode}",
                    result.ErrorCode,
                    result.Detail);
            }

            success = true;
            return AgentResult.Ok(result.PayloadJson ?? string.Empty);
        }
        catch (KeyNotFoundException ex)
        {
            _instrumentation?.RecordAdapterFailure(
                AgentId,
                capability.CapabilityKey,
                adapterType,
                "TOOL_ADAPTER_UNAVAILABLE");

            Logger.LogWarning(ex,
                "AgentNativeAdapterUnavailable | AgentId: {AgentId} | CapabilityKey: {CapabilityKey} | AdapterType: {AdapterType}",
                AgentId, capability.CapabilityKey, adapterType);

            return AgentResult.Fail(
                "Tool adapter is not registered for this capability.",
                "TOOL_ADAPTER_UNAVAILABLE",
                new { capabilityKey = capability.CapabilityKey, adapterType });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _instrumentation?.RecordAdapterFailure(
                AgentId,
                capability.CapabilityKey,
                adapterType,
                ex.GetType().Name);

            Logger.LogError(ex,
                "AgentNativePathError | AgentId: {AgentId} | CapabilityKey: {CapabilityKey}",
                AgentId, capability.CapabilityKey);

            return AgentResult.Fail($"Native execution failed: {ex.Message}");
        }
        finally
        {
            sw.Stop();
            _instrumentation?.RecordNativeExecution(
                AgentId,
                capability.CapabilityKey,
                adapterType,
                sw.Elapsed,
                success);
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
