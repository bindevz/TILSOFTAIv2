using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.Metrics;

namespace TILSOFTAI.Orchestration.Observability;

public sealed class RuntimeExecutionInstrumentation
{
    private readonly IMetricsService _metrics;
    private readonly ILogger<RuntimeExecutionInstrumentation> _logger;

    public RuntimeExecutionInstrumentation(
        IMetricsService metrics,
        ILogger<RuntimeExecutionInstrumentation> logger)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RecordSupervisorExecution(
        string agentId,
        string? domainHint,
        TimeSpan duration,
        bool success)
    {
        var labels = new Dictionary<string, string>
        {
            ["agent"] = Normalize(agentId),
            ["domain"] = Normalize(domainHint),
            ["success"] = ToLabel(success)
        };

        _metrics.IncrementCounter(MetricNames.RuntimeSupervisorExecutionsTotal, labels);
        _metrics.RecordHistogram(MetricNames.RuntimeExecutionDurationSeconds, duration.TotalSeconds, WithPath(labels, "supervisor"));

        _logger.LogInformation(
            "RuntimeExecutionObserved | Path: supervisor | AgentId: {AgentId} | Domain: {Domain} | DurationMs: {DurationMs} | Success: {Success}",
            Normalize(agentId),
            Normalize(domainHint),
            duration.TotalMilliseconds,
            success);
    }

    public void RecordNativeExecution(
        string agentId,
        string capabilityKey,
        string adapterType,
        TimeSpan duration,
        bool success)
    {
        var labels = CapabilityLabels(agentId, capabilityKey, adapterType, success);
        _metrics.IncrementCounter(MetricNames.RuntimeNativeExecutionsTotal, labels);
        _metrics.IncrementCounter(MetricNames.RuntimeCapabilityInvocationsTotal, labels);
        _metrics.RecordHistogram(MetricNames.RuntimeExecutionDurationSeconds, duration.TotalSeconds, WithPath(labels, "native"));

        _logger.LogInformation(
            "RuntimeExecutionObserved | Path: native | AgentId: {AgentId} | CapabilityKey: {CapabilityKey} | AdapterType: {AdapterType} | DurationMs: {DurationMs} | Success: {Success}",
            Normalize(agentId),
            Normalize(capabilityKey),
            Normalize(adapterType),
            duration.TotalMilliseconds,
            success);
    }

    public void RecordBridgeFallback(
        string agentId,
        string reason,
        TimeSpan duration,
        bool success)
    {
        var labels = new Dictionary<string, string>
        {
            ["agent"] = Normalize(agentId),
            ["reason"] = Normalize(reason),
            ["success"] = ToLabel(success)
        };

        _metrics.IncrementCounter(MetricNames.RuntimeBridgeFallbackTotal, labels);
        _metrics.RecordHistogram(MetricNames.RuntimeExecutionDurationSeconds, duration.TotalSeconds, WithPath(labels, "bridge"));

        _logger.LogInformation(
            "RuntimeExecutionObserved | Path: bridge | AgentId: {AgentId} | Reason: {Reason} | DurationMs: {DurationMs} | Success: {Success}",
            Normalize(agentId),
            Normalize(reason),
            duration.TotalMilliseconds,
            success);
    }

    public void RecordApprovalExecution(
        string operation,
        string adapterType,
        TimeSpan duration,
        bool success)
    {
        var labels = new Dictionary<string, string>
        {
            ["operation"] = Normalize(operation),
            ["adapter"] = Normalize(adapterType),
            ["success"] = ToLabel(success)
        };

        _metrics.IncrementCounter(MetricNames.RuntimeApprovalExecutionsTotal, labels);
        _metrics.RecordHistogram(MetricNames.RuntimeExecutionDurationSeconds, duration.TotalSeconds, WithPath(labels, "approval"));

        _logger.LogInformation(
            "RuntimeExecutionObserved | Path: approval | Operation: {Operation} | AdapterType: {AdapterType} | DurationMs: {DurationMs} | Success: {Success}",
            Normalize(operation),
            Normalize(adapterType),
            duration.TotalMilliseconds,
            success);
    }

    public void RecordAdapterFailure(string agentId, string capabilityKey, string adapterType, string? errorCode)
    {
        var labels = new Dictionary<string, string>
        {
            ["agent"] = Normalize(agentId),
            ["capability"] = Normalize(capabilityKey),
            ["adapter"] = Normalize(adapterType),
            ["error"] = Normalize(errorCode)
        };

        _metrics.IncrementCounter(MetricNames.RuntimeAdapterFailuresTotal, labels);

        _logger.LogWarning(
            "RuntimeAdapterFailureObserved | AgentId: {AgentId} | CapabilityKey: {CapabilityKey} | AdapterType: {AdapterType} | ErrorCode: {ErrorCode}",
            Normalize(agentId),
            Normalize(capabilityKey),
            Normalize(adapterType),
            Normalize(errorCode));
    }

    private static Dictionary<string, string> CapabilityLabels(
        string agentId,
        string capabilityKey,
        string adapterType,
        bool success) => new(StringComparer.OrdinalIgnoreCase)
        {
            ["agent"] = Normalize(agentId),
            ["capability"] = Normalize(capabilityKey),
            ["adapter"] = Normalize(adapterType),
            ["success"] = ToLabel(success)
        };

    private static Dictionary<string, string> WithPath(Dictionary<string, string> labels, string path)
    {
        var copy = new Dictionary<string, string>(labels, StringComparer.OrdinalIgnoreCase)
        {
            ["path"] = path
        };
        return copy;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value.Trim().ToLowerInvariant();

    private static string ToLabel(bool value) => value ? "true" : "false";
}
