using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class PlatformCatalogControlPlane : IPlatformCatalogControlPlane
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IPlatformCatalogMutationStore _store;
    private readonly IAuditLogger _auditLogger;
    private readonly IMetricsService _metrics;
    private readonly CatalogControlPlaneOptions _options;
    private readonly ILogger<PlatformCatalogControlPlane> _logger;

    public PlatformCatalogControlPlane(
        IPlatformCatalogMutationStore store,
        IAuditLogger auditLogger,
        IMetricsService metrics,
        IOptions<CatalogControlPlaneOptions> options,
        ILogger<PlatformCatalogControlPlane> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<CapabilityDescriptor>> ListCapabilitiesAsync(CatalogMutationContext context, CancellationToken ct)
    {
        RequireRole(context, _options.SubmitRoles, "catalog.capability.list");
        return _store.ListCapabilitiesAsync(ct);
    }

    public Task<IReadOnlyList<KeyValuePair<string, ExternalConnectionOptions>>> ListExternalConnectionsAsync(
        CatalogMutationContext context,
        CancellationToken ct)
    {
        RequireRole(context, _options.SubmitRoles, "catalog.external_connection.list");
        return _store.ListExternalConnectionsAsync(ct);
    }

    public Task<IReadOnlyList<CatalogChangeRequestRecord>> ListChangesAsync(CatalogMutationContext context, CancellationToken ct)
    {
        RequireRole(context, _options.SubmitRoles.Concat(_options.ApproveRoles).ToArray(), "catalog.change.list");
        return _store.ListChangesAsync(context.TenantId, ct);
    }

    public async Task<CatalogChangeRequestRecord> ProposeAsync(
        CatalogMutationRequest request,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            RequireRole(context, _options.SubmitRoles, "catalog.change.propose");
            ArgumentNullException.ThrowIfNull(request);

            var record = await BuildChangeRecordAsync(request, context, ct);
            var created = await _store.CreateChangeAsync(record, ct);

            RecordMutation("propose", record.RecordType, success: true);
            Audit(context, "catalog.change.propose", record, AuditOutcome.Success);
            _logger.LogInformation(
                "PlatformCatalogMutationProposed | ChangeId: {ChangeId} | Type: {RecordType} | Operation: {Operation} | Key: {RecordKey} | User: {UserId} | DurationMs: {DurationMs}",
                created.ChangeId,
                created.RecordType,
                created.Operation,
                created.RecordKey,
                context.UserId,
                sw.ElapsedMilliseconds);

            return created;
        }
        catch
        {
            RecordMutation("propose", NormalizeRecordType(request?.RecordType), success: false);
            throw;
        }
    }

    public async Task<CatalogChangeRequestRecord> ApproveAsync(
        string changeId,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        RequireRole(context, _options.ApproveRoles, "catalog.change.approve");
        var existing = await RequireChangeAsync(changeId, context, ct);
        EnsurePending(existing);

        if (!_options.AllowSelfApproval
            && string.Equals(existing.RequestedByUserId, context.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Catalog changes require an independent reviewer.");
        }

        var approved = await _store.ApproveChangeAsync(context.TenantId, changeId, context.UserId, ct);
        RecordMutation("approve", approved.RecordType, success: true);
        Audit(context, "catalog.change.approve", approved, AuditOutcome.Success);
        return approved;
    }

    public async Task<CatalogChangeRequestRecord> RejectAsync(
        string changeId,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        RequireRole(context, _options.ApproveRoles, "catalog.change.reject");
        var existing = await RequireChangeAsync(changeId, context, ct);
        EnsurePending(existing);

        var rejected = await _store.RejectChangeAsync(context.TenantId, changeId, context.UserId, ct);
        RecordMutation("reject", rejected.RecordType, success: true);
        Audit(context, "catalog.change.reject", rejected, AuditOutcome.Success);
        return rejected;
    }

    public async Task<CatalogChangeRequestRecord> ApplyAsync(
        string changeId,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        RequireRole(context, _options.ApproveRoles, "catalog.change.apply");
        var change = await RequireChangeAsync(changeId, context, ct);
        if (!string.Equals(change.Status, PlatformCatalogChangeStatus.Approved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Catalog change must be approved before apply.");
        }

        var applyingChange = WithApplicationMetadata(change, context.UserId);
        switch (change.RecordType.ToLowerInvariant())
        {
            case PlatformCatalogRecordTypes.Capability:
                if (string.Equals(change.Operation, PlatformCatalogOperations.Disable, StringComparison.OrdinalIgnoreCase))
                {
                    await _store.DisableCapabilityAsync(change.RecordKey, applyingChange, ct);
                }
                else
                {
                    var capability = JsonSerializer.Deserialize<CapabilityDescriptor>(change.PayloadJson, JsonOptions)
                        ?? throw new InvalidOperationException("Catalog change payload is not a capability record.");
                    await _store.UpsertCapabilityAsync(capability, applyingChange, ct);
                }
                break;

            case PlatformCatalogRecordTypes.ExternalConnection:
                if (string.Equals(change.Operation, PlatformCatalogOperations.Disable, StringComparison.OrdinalIgnoreCase))
                {
                    await _store.DisableExternalConnectionAsync(change.RecordKey, applyingChange, ct);
                }
                else
                {
                    var connection = JsonSerializer.Deserialize<ExternalConnectionOptions>(change.PayloadJson, JsonOptions)
                        ?? throw new InvalidOperationException("Catalog change payload is not an external connection record.");
                    await _store.UpsertExternalConnectionAsync(change.RecordKey, connection, applyingChange, ct);
                }
                break;

            default:
                throw new InvalidOperationException($"Unsupported catalog record type: {change.RecordType}");
        }

        var applied = await _store.MarkAppliedAsync(context.TenantId, changeId, context.UserId, ct);
        RecordMutation("apply", applied.RecordType, success: true);
        Audit(context, "catalog.change.apply", applied, AuditOutcome.Success);
        return applied;
    }

    private static CatalogChangeRequestRecord WithApplicationMetadata(
        CatalogChangeRequestRecord change,
        string appliedByUserId) => new()
    {
        ChangeId = change.ChangeId,
        TenantId = change.TenantId,
        RecordType = change.RecordType,
        Operation = change.Operation,
        RecordKey = change.RecordKey,
        PayloadJson = change.PayloadJson,
        Status = change.Status,
        Owner = change.Owner,
        ChangeNote = change.ChangeNote,
        VersionTag = change.VersionTag,
        RequestedByUserId = change.RequestedByUserId,
        RequestedAtUtc = change.RequestedAtUtc,
        ReviewedByUserId = change.ReviewedByUserId,
        ReviewedAtUtc = change.ReviewedAtUtc,
        AppliedByUserId = appliedByUserId,
        AppliedAtUtc = DateTime.UtcNow
    };

    private async Task<CatalogChangeRequestRecord> BuildChangeRecordAsync(
        CatalogMutationRequest request,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Owner))
        {
            throw new ArgumentException("Catalog change owner is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ChangeNote))
        {
            throw new ArgumentException("Catalog change note is required.", nameof(request));
        }

        var recordType = NormalizeRecordType(request.RecordType);
        var operation = NormalizeOperation(request.Operation);
        var key = request.RecordKey;
        string payloadJson;

        if (recordType == PlatformCatalogRecordTypes.Capability)
        {
            if (operation == PlatformCatalogOperations.Disable)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Capability key is required for disable.", nameof(request));
                }

                payloadJson = "{}";
            }
            else
            {
                var capability = request.Capability
                    ?? throw new ArgumentException("Capability record is required.", nameof(request));
                key = capability.CapabilityKey;
                var connections = (await _store.ListExternalConnectionsAsync(ct))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                var validation = CatalogIntegrityValidator.ValidateCapabilityMutation(capability, connections);
                if (!validation.IsValid)
                {
                    throw new ArgumentException(CatalogIntegrityValidator.SerializeErrors(validation.Errors), nameof(request));
                }

                payloadJson = JsonSerializer.Serialize(capability, JsonOptions);
            }
        }
        else if (recordType == PlatformCatalogRecordTypes.ExternalConnection)
        {
            if (operation == PlatformCatalogOperations.Disable)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Connection name is required for disable.", nameof(request));
                }

                payloadJson = "{}";
            }
            else
            {
                var connection = request.ExternalConnection
                    ?? throw new ArgumentException("External connection record is required.", nameof(request));
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Connection name is required.", nameof(request));
                }

                var validation = CatalogIntegrityValidator.ValidateConnectionMutation(key, connection);
                if (!validation.IsValid)
                {
                    throw new ArgumentException(CatalogIntegrityValidator.SerializeErrors(validation.Errors), nameof(request));
                }

                payloadJson = JsonSerializer.Serialize(connection, JsonOptions);
            }
        }
        else
        {
            throw new ArgumentException($"Unsupported catalog record type: {request.RecordType}", nameof(request));
        }

        return new CatalogChangeRequestRecord
        {
            ChangeId = Guid.NewGuid().ToString("N"),
            TenantId = context.TenantId,
            RecordType = recordType,
            Operation = operation,
            RecordKey = key,
            PayloadJson = payloadJson,
            Status = PlatformCatalogChangeStatus.Pending,
            Owner = request.Owner,
            ChangeNote = request.ChangeNote,
            VersionTag = request.VersionTag,
            RequestedByUserId = context.UserId,
            RequestedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<CatalogChangeRequestRecord> RequireChangeAsync(
        string changeId,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(changeId))
        {
            throw new ArgumentException("Change id is required.", nameof(changeId));
        }

        return await _store.GetChangeAsync(context.TenantId, changeId, ct)
            ?? throw new InvalidOperationException("Catalog change request was not found.");
    }

    private static void EnsurePending(CatalogChangeRequestRecord change)
    {
        if (!string.Equals(change.Status, PlatformCatalogChangeStatus.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Catalog change is not pending review.");
        }
    }

    private void RequireRole(CatalogMutationContext context, IReadOnlyList<string> requiredRoles, string action)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (requiredRoles.Count == 0
            || requiredRoles.Any(required => context.Roles.Contains(required, StringComparer.OrdinalIgnoreCase)))
        {
            _auditLogger.LogGovernanceEvent(GovernanceAuditEvent.Allowed(
                context.TenantId,
                context.UserId,
                context.CorrelationId,
                action,
                "catalog_control_plane",
                context.Roles.ToArray(),
                requiredRoles.ToArray(),
                durationMs: 0));
            return;
        }

        _auditLogger.LogGovernanceEvent(GovernanceAuditEvent.Denied(
            context.TenantId,
            context.UserId,
            context.CorrelationId,
            action,
            "catalog_control_plane",
            context.Roles.ToArray(),
            requiredRoles.ToArray(),
            "missing_required_catalog_role",
            "CATALOG_CONTROL_PLANE_DENIED",
            durationMs: 0));

        throw new UnauthorizedAccessException(
            $"User does not have required catalog control-plane roles ({string.Join(", ", requiredRoles)}).");
    }

    private void Audit(
        CatalogMutationContext context,
        string action,
        CatalogChangeRequestRecord record,
        AuditOutcome outcome)
    {
        _auditLogger.Log(new AuditEvent
        {
            EventType = AuditEventType.Admin_ConfigChange,
            TenantId = context.TenantId,
            UserId = context.UserId,
            CorrelationId = context.CorrelationId,
            Outcome = outcome,
            Details = JsonSerializer.SerializeToDocument(new
            {
                action,
                record.ChangeId,
                record.RecordType,
                record.Operation,
                record.RecordKey,
                record.Owner,
                record.ChangeNote,
                record.VersionTag,
                record.Status
            })
        });
    }

    private void RecordMutation(string operation, string recordType, bool success)
    {
        _metrics.IncrementCounter(MetricNames.PlatformCatalogMutationTotal, new Dictionary<string, string>
        {
            ["operation"] = operation,
            ["record_type"] = recordType,
            ["success"] = success ? "true" : "false"
        });
    }

    private static string NormalizeRecordType(string? recordType) =>
        recordType?.Trim().ToLowerInvariant() switch
        {
            PlatformCatalogRecordTypes.Capability => PlatformCatalogRecordTypes.Capability,
            "connection" => PlatformCatalogRecordTypes.ExternalConnection,
            "externalconnection" => PlatformCatalogRecordTypes.ExternalConnection,
            PlatformCatalogRecordTypes.ExternalConnection => PlatformCatalogRecordTypes.ExternalConnection,
            _ => recordType?.Trim().ToLowerInvariant() ?? string.Empty
        };

    private static string NormalizeOperation(string? operation) =>
        operation?.Trim().ToLowerInvariant() switch
        {
            PlatformCatalogOperations.Disable => PlatformCatalogOperations.Disable,
            _ => PlatformCatalogOperations.Upsert
        };
}
