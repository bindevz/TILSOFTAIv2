using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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

    public async Task<CatalogMutationPreviewResult> PreviewAsync(
        CatalogMutationRequest request,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        RequireRole(context, _options.SubmitRoles, "catalog.change.preview");
        var plan = await BuildMutationPlanAsync(request, context, ct);
        RecordMutation("preview", plan.RecordType, plan.RiskLevel, success: plan.IsValid);
        return plan.ToPreviewResult();
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

            var plan = await BuildMutationPlanAsync(request, context, ct);
            if (plan.DuplicatePendingChange is not null)
            {
                RecordMutation("propose_duplicate", plan.RecordType, plan.RiskLevel, success: true);
                Audit(context, "catalog.change.propose_duplicate", plan.DuplicatePendingChange, AuditOutcome.Success);
                return plan.DuplicatePendingChange;
            }

            if (!plan.IsValid)
            {
                throw new ArgumentException(CatalogIntegrityValidator.SerializeErrors(plan.Errors), nameof(request));
            }

            var record = plan.ToChangeRecord(context);
            var created = await _store.CreateChangeAsync(record, ct);

            RecordMutation("propose", record.RecordType, record.RiskLevel, success: true);
            Audit(context, "catalog.change.propose", record, AuditOutcome.Success);
            _logger.LogInformation(
                "PlatformCatalogMutationProposed | ChangeId: {ChangeId} | Type: {RecordType} | Operation: {Operation} | Key: {RecordKey} | Risk: {RiskLevel} | ExpectedVersion: {ExpectedVersionTag} | User: {UserId} | DurationMs: {DurationMs}",
                created.ChangeId,
                created.RecordType,
                created.Operation,
                created.RecordKey,
                created.RiskLevel,
                created.ExpectedVersionTag,
                context.UserId,
                sw.ElapsedMilliseconds);

            return created;
        }
        catch
        {
            RecordMutation("propose", NormalizeRecordType(request?.RecordType), CatalogChangeRiskLevels.Standard, success: false);
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
        RequireChangeRiskPolicy(context, existing, "catalog.change.approve");

        if (!_options.AllowSelfApproval
            && string.Equals(existing.RequestedByUserId, context.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Catalog changes require an independent reviewer.");
        }

        var approved = await _store.ApproveChangeAsync(context.TenantId, changeId, context.UserId, ct);
        RecordMutation("approve", approved.RecordType, approved.RiskLevel, success: true);
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
        RecordMutation("reject", rejected.RecordType, rejected.RiskLevel, success: true);
        Audit(context, "catalog.change.reject", rejected, AuditOutcome.Success);
        return rejected;
    }

    public async Task<CatalogChangeRequestRecord> ApplyAsync(
        string changeId,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        RequireRole(context, _options.ApplyRoles, "catalog.change.apply");
        var change = await RequireChangeAsync(changeId, context, ct);
        if (string.Equals(change.Status, PlatformCatalogChangeStatus.Applied, StringComparison.OrdinalIgnoreCase))
        {
            RecordMutation("apply_replay", change.RecordType, change.RiskLevel, success: true);
            Audit(context, "catalog.change.apply_replay", change, AuditOutcome.Success);
            return change;
        }

        if (!string.Equals(change.Status, PlatformCatalogChangeStatus.Approved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Catalog change must be approved before apply.");
        }

        if (IsProductionLike()
            && _options.RequireIndependentApplyInProductionLike
            && string.Equals(change.ReviewedByUserId, context.UserId, StringComparison.OrdinalIgnoreCase)
            && !HasBreakGlass(context, change))
        {
            throw new UnauthorizedAccessException("Production-like catalog changes require independent apply after review.");
        }

        await EnsureVersionSafeForApplyAsync(change, ct);

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
        RecordMutation("apply", applied.RecordType, applied.RiskLevel, success: true);
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
        ExpectedVersionTag = change.ExpectedVersionTag,
        IdempotencyKey = change.IdempotencyKey,
        RollbackOfChangeId = change.RollbackOfChangeId,
        PayloadHash = change.PayloadHash,
        RiskLevel = change.RiskLevel,
        EnvironmentName = change.EnvironmentName,
        BreakGlass = change.BreakGlass,
        BreakGlassJustification = change.BreakGlassJustification,
        RequestedByUserId = change.RequestedByUserId,
        RequestedAtUtc = change.RequestedAtUtc,
        ReviewedByUserId = change.ReviewedByUserId,
        ReviewedAtUtc = change.ReviewedAtUtc,
        AppliedByUserId = appliedByUserId,
        AppliedAtUtc = DateTime.UtcNow
    };

    private async Task<CatalogMutationPlan> BuildMutationPlanAsync(
        CatalogMutationRequest request,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Owner))
        {
            errors.Add("catalog_owner_required");
        }

        if (string.IsNullOrWhiteSpace(request.ChangeNote))
        {
            errors.Add("catalog_change_note_required");
        }

        var recordType = NormalizeRecordType(request.RecordType);
        var operation = NormalizeOperation(request.Operation);
        var key = request.RecordKey;
        var payloadJson = "{}";

        if (recordType == PlatformCatalogRecordTypes.Capability)
        {
            if (operation == PlatformCatalogOperations.Disable)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    errors.Add("capability_key_required_for_disable");
                }
            }
            else
            {
                if (request.Capability is null)
                {
                    errors.Add("capability_record_required");
                }
                else
                {
                    key = request.Capability.CapabilityKey;
                    var connections = (await _store.ListExternalConnectionsAsync(ct))
                        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                    var validation = CatalogIntegrityValidator.ValidateCapabilityMutation(request.Capability, connections);
                    errors.AddRange(validation.Errors);

                    payloadJson = JsonSerializer.Serialize(request.Capability, JsonOptions);
                }
            }
        }
        else if (recordType == PlatformCatalogRecordTypes.ExternalConnection)
        {
            if (operation == PlatformCatalogOperations.Disable)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    errors.Add("connection_name_required_for_disable");
                }
            }
            else
            {
                if (request.ExternalConnection is null)
                {
                    errors.Add("external_connection_record_required");
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    errors.Add("connection_name_required");
                }

                if (request.ExternalConnection is not null && !string.IsNullOrWhiteSpace(key))
                {
                    var validation = CatalogIntegrityValidator.ValidateConnectionMutation(key, request.ExternalConnection);
                    errors.AddRange(validation.Errors);
                    payloadJson = JsonSerializer.Serialize(request.ExternalConnection, JsonOptions);
                }
            }
        }
        else
        {
            errors.Add($"unsupported_catalog_record_type:{request.RecordType}");
        }

        var riskLevel = DetermineRisk(recordType, operation);
        var payloadHash = ComputePayloadHash(recordType, operation, key, payloadJson);
        var current = string.IsNullOrWhiteSpace(key)
            ? new CatalogRecordVersion()
            : await _store.GetRecordVersionAsync(recordType, key, ct);

        if (current.Exists)
        {
            if (!string.IsNullOrWhiteSpace(request.ExpectedVersionTag)
                && !string.Equals(current.VersionTag, request.ExpectedVersionTag, StringComparison.Ordinal))
            {
                errors.Add($"catalog_version_conflict:{recordType}:{key}:expected={request.ExpectedVersionTag}:actual={current.VersionTag}");
            }
            else if (RequiresExpectedVersion() && string.IsNullOrWhiteSpace(request.ExpectedVersionTag))
            {
                errors.Add($"catalog_expected_version_required:{recordType}:{key}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.ExpectedVersionTag))
        {
            errors.Add($"catalog_expected_version_record_missing:{recordType}:{key}");
        }

        if (operation == PlatformCatalogOperations.Disable && !current.Exists)
        {
            warnings.Add($"catalog_disable_target_missing:{recordType}:{key}");
        }

        var duplicate = string.IsNullOrWhiteSpace(key)
            ? null
            : await _store.FindDuplicatePendingChangeAsync(
                context.TenantId,
                recordType,
                operation,
                key,
                payloadHash,
                request.IdempotencyKey,
                ct);

        if (duplicate is not null)
        {
            warnings.Add($"catalog_duplicate_pending_change:{duplicate.ChangeId}");
        }

        if (request.BreakGlass && !HasBreakGlass(context, request))
        {
            errors.Add("catalog_break_glass_not_permitted");
        }

        return new CatalogMutationPlan
        {
            RecordType = recordType,
            Operation = operation,
            RecordKey = key,
            PayloadJson = payloadJson,
            Owner = request.Owner,
            ChangeNote = request.ChangeNote,
            VersionTag = request.VersionTag,
            ExpectedVersionTag = request.ExpectedVersionTag,
            IdempotencyKey = request.IdempotencyKey,
            RollbackOfChangeId = request.RollbackOfChangeId,
            PayloadHash = payloadHash,
            RiskLevel = riskLevel,
            EnvironmentName = EffectiveEnvironmentName(),
            BreakGlass = request.BreakGlass,
            BreakGlassJustification = request.BreakGlassJustification,
            CurrentVersion = current,
            DuplicatePendingChange = duplicate,
            Errors = errors,
            Warnings = warnings
        };
    }

    private async Task EnsureVersionSafeForApplyAsync(CatalogChangeRequestRecord change, CancellationToken ct)
    {
        var current = await _store.GetRecordVersionAsync(change.RecordType, change.RecordKey, ct);
        if (current.Exists)
        {
            if (!string.IsNullOrWhiteSpace(change.ExpectedVersionTag)
                && !string.Equals(current.VersionTag, change.ExpectedVersionTag, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Catalog record version conflict for {change.RecordType}:{change.RecordKey}. Expected {change.ExpectedVersionTag}; actual {current.VersionTag}.");
            }

            if (RequiresExpectedVersion() && string.IsNullOrWhiteSpace(change.ExpectedVersionTag))
            {
                throw new InvalidOperationException(
                    $"Catalog record {change.RecordType}:{change.RecordKey} requires ExpectedVersionTag in production-like environments.");
            }
        }
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
                record.ExpectedVersionTag,
                record.IdempotencyKey,
                record.RollbackOfChangeId,
                record.PayloadHash,
                record.RiskLevel,
                record.EnvironmentName,
                record.BreakGlass,
                record.Status
            })
        });
    }

    private void RecordMutation(string operation, string recordType, string riskLevel, bool success)
    {
        _metrics.IncrementCounter(MetricNames.PlatformCatalogMutationTotal, new Dictionary<string, string>
        {
            ["operation"] = operation,
            ["record_type"] = recordType,
            ["risk_level"] = riskLevel,
            ["environment"] = EffectiveEnvironmentName(),
            ["success"] = success ? "true" : "false"
        });
    }

    private void RequireChangeRiskPolicy(CatalogMutationContext context, CatalogChangeRequestRecord change, string action)
    {
        if (string.Equals(change.RiskLevel, CatalogChangeRiskLevels.High, StringComparison.OrdinalIgnoreCase)
            && !_options.HighRiskApproveRoles.Any(required => context.Roles.Contains(required, StringComparer.OrdinalIgnoreCase))
            && !HasBreakGlass(context, change))
        {
            _auditLogger.LogGovernanceEvent(GovernanceAuditEvent.Denied(
                context.TenantId,
                context.UserId,
                context.CorrelationId,
                action,
                "catalog_control_plane",
                context.Roles.ToArray(),
                _options.HighRiskApproveRoles,
                "missing_high_risk_catalog_role",
                "CATALOG_HIGH_RISK_DENIED",
                durationMs: 0));

            throw new UnauthorizedAccessException(
                $"High-risk catalog changes require one of these roles: {string.Join(", ", _options.HighRiskApproveRoles)}.");
        }
    }

    private bool HasBreakGlass(CatalogMutationContext context, CatalogChangeRequestRecord change) =>
        change.BreakGlass
        && HasBreakGlass(context, new CatalogMutationRequest
        {
            BreakGlass = change.BreakGlass,
            BreakGlassJustification = change.BreakGlassJustification
        });

    private bool HasBreakGlass(CatalogMutationContext context, CatalogMutationRequest request) =>
        _options.AllowBreakGlass
        && request.BreakGlass
        && request.BreakGlassJustification.Trim().Length >= _options.MinBreakGlassJustificationLength
        && _options.BreakGlassRoles.Any(required => context.Roles.Contains(required, StringComparer.OrdinalIgnoreCase));

    private bool RequiresExpectedVersion() =>
        IsProductionLike() && _options.RequireExpectedVersionForExistingRecordsInProductionLike;

    private bool IsProductionLike() =>
        _options.ProductionLikeEnvironments.Any(environment =>
            string.Equals(environment, EffectiveEnvironmentName(), StringComparison.OrdinalIgnoreCase));

    private string EffectiveEnvironmentName() =>
        string.IsNullOrWhiteSpace(_options.EnvironmentName) ? "development" : _options.EnvironmentName.Trim();

    private static string DetermineRisk(string recordType, string operation) =>
        string.Equals(operation, PlatformCatalogOperations.Disable, StringComparison.OrdinalIgnoreCase)
        || string.Equals(recordType, PlatformCatalogRecordTypes.ExternalConnection, StringComparison.OrdinalIgnoreCase)
            ? CatalogChangeRiskLevels.High
            : CatalogChangeRiskLevels.Standard;

    private static string ComputePayloadHash(string recordType, string operation, string recordKey, string payloadJson)
    {
        var payload = $"{recordType.Trim().ToLowerInvariant()}|{operation.Trim().ToLowerInvariant()}|{recordKey.Trim().ToLowerInvariant()}|{payloadJson}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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

    private sealed class CatalogMutationPlan
    {
        public string RecordType { get; init; } = string.Empty;
        public string Operation { get; init; } = string.Empty;
        public string RecordKey { get; init; } = string.Empty;
        public string PayloadJson { get; init; } = "{}";
        public string Owner { get; init; } = string.Empty;
        public string ChangeNote { get; init; } = string.Empty;
        public string VersionTag { get; init; } = string.Empty;
        public string ExpectedVersionTag { get; init; } = string.Empty;
        public string IdempotencyKey { get; init; } = string.Empty;
        public string RollbackOfChangeId { get; init; } = string.Empty;
        public string PayloadHash { get; init; } = string.Empty;
        public string RiskLevel { get; init; } = CatalogChangeRiskLevels.Standard;
        public string EnvironmentName { get; init; } = string.Empty;
        public bool BreakGlass { get; init; }
        public string BreakGlassJustification { get; init; } = string.Empty;
        public CatalogRecordVersion CurrentVersion { get; init; } = new();
        public CatalogChangeRequestRecord? DuplicatePendingChange { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public bool IsValid => Errors.Count == 0;

        public CatalogMutationPreviewResult ToPreviewResult() => new()
        {
            IsValid = IsValid,
            RecordType = RecordType,
            Operation = Operation,
            RecordKey = RecordKey,
            RiskLevel = RiskLevel,
            EnvironmentName = EnvironmentName,
            ExistingRecordFound = CurrentVersion.Exists,
            CurrentVersionTag = CurrentVersion.VersionTag,
            ExpectedVersionTag = ExpectedVersionTag,
            PayloadHash = PayloadHash,
            DuplicatePendingChangeId = DuplicatePendingChange?.ChangeId,
            Errors = Errors,
            Warnings = Warnings
        };

        public CatalogChangeRequestRecord ToChangeRecord(CatalogMutationContext context) => new()
        {
            ChangeId = Guid.NewGuid().ToString("N"),
            TenantId = context.TenantId,
            RecordType = RecordType,
            Operation = Operation,
            RecordKey = RecordKey,
            PayloadJson = PayloadJson,
            Status = PlatformCatalogChangeStatus.Pending,
            Owner = Owner,
            ChangeNote = ChangeNote,
            VersionTag = VersionTag,
            ExpectedVersionTag = ExpectedVersionTag,
            IdempotencyKey = IdempotencyKey,
            RollbackOfChangeId = RollbackOfChangeId,
            PayloadHash = PayloadHash,
            RiskLevel = RiskLevel,
            EnvironmentName = EnvironmentName,
            BreakGlass = BreakGlass,
            BreakGlassJustification = BreakGlassJustification,
            RequestedByUserId = context.UserId,
            RequestedAtUtc = DateTime.UtcNow
        };
    }
}
