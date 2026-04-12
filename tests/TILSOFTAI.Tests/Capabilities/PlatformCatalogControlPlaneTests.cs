using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Orchestration.Capabilities;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class PlatformCatalogControlPlaneTests
{
    [Fact]
    public async Task ProposeAsync_ShouldCreatePendingChange_WhenRoleAndMetadataAreValid()
    {
        var store = new InMemoryCatalogStore();
        store.Connections["external-stock-api"] = new ExternalConnectionOptions { BaseUrl = "https://stock.test" };
        var controlPlane = CreateControlPlane(store);

        var change = await controlPlane.ProposeAsync(
            CapabilityUpsert("warehouse.external-stock.lookup"),
            Submitter(),
            CancellationToken.None);

        change.Status.Should().Be(PlatformCatalogChangeStatus.Pending);
        change.Owner.Should().Be("platform");
        change.ChangeNote.Should().Be("test change");
        change.ExpectedVersionTag.Should().BeEmpty();
        change.PayloadHash.Should().NotBeNullOrWhiteSpace();
        change.RiskLevel.Should().Be(CatalogChangeRiskLevels.Standard);
        store.Changes.Should().ContainKey(change.ChangeId);
    }

    [Fact]
    public async Task PreviewAsync_ShouldValidateWithoutCreatingChange()
    {
        var store = new InMemoryCatalogStore();
        var controlPlane = CreateControlPlane(store);

        var preview = await controlPlane.PreviewAsync(
            CapabilityUpsert("warehouse.inventory.summary"),
            Submitter(),
            CancellationToken.None);

        preview.IsValid.Should().BeTrue();
        preview.RecordKey.Should().Be("warehouse.inventory.summary");
        preview.PayloadHash.Should().NotBeNullOrWhiteSpace();
        store.Changes.Should().BeEmpty();
    }

    [Fact]
    public async Task ProposeAsync_ShouldReturnExistingPendingChange_WhenDuplicatePayloadIsSubmitted()
    {
        var store = new InMemoryCatalogStore();
        var controlPlane = CreateControlPlane(store);
        var request = CapabilityUpsert("warehouse.inventory.summary").withIdempotency("same-request");

        var first = await controlPlane.ProposeAsync(request, Submitter(), CancellationToken.None);
        var second = await controlPlane.ProposeAsync(request, Submitter(), CancellationToken.None);

        second.ChangeId.Should().Be(first.ChangeId);
        store.Changes.Should().ContainSingle();
    }

    [Fact]
    public async Task ProposeAsync_ShouldRejectCallerWithoutSubmitRole()
    {
        var controlPlane = CreateControlPlane(new InMemoryCatalogStore());

        var act = () => controlPlane.ProposeAsync(
            CapabilityUpsert("warehouse.inventory.summary"),
            new CatalogMutationContext { TenantId = "t1", UserId = "u1", Roles = Array.Empty<string>() },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ApproveAsync_ShouldRejectSelfApproval()
    {
        var store = new InMemoryCatalogStore();
        var controlPlane = CreateControlPlane(store);
        var submitterAndApprover = new CatalogMutationContext
        {
            TenantId = "t1",
            UserId = "user-1",
            Roles = new[] { "platform_catalog_admin", "platform_catalog_approver" },
            CorrelationId = "c1"
        };

        var change = await controlPlane.ProposeAsync(
            CapabilityUpsert("warehouse.inventory.summary"),
            submitterAndApprover,
            CancellationToken.None);

        var act = () => controlPlane.ApproveAsync(change.ChangeId, submitterAndApprover, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ApplyAsync_ShouldPersistApprovedCapabilityChange()
    {
        var store = new InMemoryCatalogStore();
        var controlPlane = CreateControlPlane(store);
        var change = await controlPlane.ProposeAsync(
            CapabilityUpsert("warehouse.inventory.summary"),
            Submitter(),
            CancellationToken.None);

        await controlPlane.ApproveAsync(change.ChangeId, Approver(), CancellationToken.None);
        var applied = await controlPlane.ApplyAsync(change.ChangeId, Approver(), CancellationToken.None);

        applied.Status.Should().Be(PlatformCatalogChangeStatus.Applied);
        store.Capabilities.Should().ContainKey("warehouse.inventory.summary");
        store.LastMutationChange!.AppliedByUserId.Should().Be("approver");
    }

    [Fact]
    public async Task ApplyAsync_ShouldBeIdempotent_WhenChangeIsAlreadyApplied()
    {
        var store = new InMemoryCatalogStore();
        var controlPlane = CreateControlPlane(store);
        var change = await controlPlane.ProposeAsync(
            CapabilityUpsert("warehouse.inventory.summary"),
            Submitter(),
            CancellationToken.None);

        await controlPlane.ApproveAsync(change.ChangeId, Approver(), CancellationToken.None);
        var first = await controlPlane.ApplyAsync(change.ChangeId, Approver(), CancellationToken.None);
        var second = await controlPlane.ApplyAsync(change.ChangeId, Approver(), CancellationToken.None);

        second.Status.Should().Be(PlatformCatalogChangeStatus.Applied);
        second.ChangeId.Should().Be(first.ChangeId);
    }

    [Fact]
    public async Task ProposeAsync_ShouldRejectVersionConflict()
    {
        var store = new InMemoryCatalogStore();
        store.Capabilities["warehouse.inventory.summary"] = new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.inventory.summary",
            Domain = "warehouse",
            AdapterType = "sql",
            Operation = "execute_query",
            TargetSystemId = "sql",
            ArgumentContract = new CapabilityArgumentContract { AllowAdditionalArguments = false }
        };
        store.Versions["capability:warehouse.inventory.summary"] = "v2";
        var controlPlane = CreateControlPlane(store);

        var act = () => controlPlane.ProposeAsync(
            CapabilityUpsert("warehouse.inventory.summary").withExpectedVersion("v1"),
            Submitter(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*catalog_version_conflict*");
    }

    [Fact]
    public async Task ApproveAsync_ShouldRequireHighRiskRole_ForExternalConnectionChange()
    {
        var store = new InMemoryCatalogStore();
        var controlPlane = CreateControlPlane(store);
        var change = await controlPlane.ProposeAsync(
            ConnectionUpsert("external-stock-api"),
            Submitter(),
            CancellationToken.None);

        var act = () => controlPlane.ApproveAsync(change.ChangeId, Approver(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*High-risk catalog changes require*");
    }

    [Fact]
    public async Task ProposeAsync_ShouldRequireExpectedVersionForExistingProductionRecord()
    {
        var store = new InMemoryCatalogStore();
        store.Capabilities["warehouse.inventory.summary"] = new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.inventory.summary",
            Domain = "warehouse",
            AdapterType = "sql",
            Operation = "execute_query",
            TargetSystemId = "sql",
            ArgumentContract = new CapabilityArgumentContract { AllowAdditionalArguments = false }
        };
        store.Versions["capability:warehouse.inventory.summary"] = "v2";
        var controlPlane = CreateControlPlane(store, new CatalogControlPlaneOptions { EnvironmentName = "prod" });

        var act = () => controlPlane.ProposeAsync(
            CapabilityUpsert("warehouse.inventory.summary"),
            Submitter(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*catalog_expected_version_required*");
    }

    [Fact]
    public async Task ProposeAsync_ShouldRejectRawSecretConnectionMetadata()
    {
        var controlPlane = CreateControlPlane(new InMemoryCatalogStore());
        var request = new CatalogMutationRequest
        {
            RecordType = PlatformCatalogRecordTypes.ExternalConnection,
            Operation = PlatformCatalogOperations.Upsert,
            RecordKey = "bad-api",
            ExternalConnection = new ExternalConnectionOptions
            {
                BaseUrl = "https://bad.test",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["apiKey"] = "raw-secret"
                }
            },
            Owner = "platform",
            ChangeNote = "test change"
        };

        var act = () => controlPlane.ProposeAsync(request, Submitter(), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*connection_raw_secret_header*");
    }

    private static PlatformCatalogControlPlane CreateControlPlane(
        InMemoryCatalogStore store,
        CatalogControlPlaneOptions? options = null) => new(
        store,
        new CapturingAuditLogger(),
        new NoopMetricsService(),
        Options.Create(options ?? new CatalogControlPlaneOptions()),
        new Mock<ILogger<PlatformCatalogControlPlane>>().Object);

    private static CatalogMutationContext Submitter() => new()
    {
        TenantId = "t1",
        UserId = "submitter",
        Roles = new[] { "platform_catalog_admin" },
        CorrelationId = "c1"
    };

    private static CatalogMutationContext Approver() => new()
    {
        TenantId = "t1",
        UserId = "approver",
        Roles = new[] { "platform_catalog_approver" },
        CorrelationId = "c1"
    };

    private static CatalogMutationRequest CapabilityUpsert(string capabilityKey) => new()
    {
        RecordType = PlatformCatalogRecordTypes.Capability,
        Operation = PlatformCatalogOperations.Upsert,
        Owner = "platform",
        ChangeNote = "test change",
        VersionTag = "test-v1",
        Capability = new CapabilityDescriptor
        {
            CapabilityKey = capabilityKey,
            Domain = "warehouse",
            AdapterType = "sql",
            Operation = "execute_query",
            TargetSystemId = "sql",
            ExecutionMode = "readonly",
            IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "dbo.ai_test"
            },
            ArgumentContract = new CapabilityArgumentContract
            {
                AllowAdditionalArguments = false
            }
        }
    };

    private static CatalogMutationRequest ConnectionUpsert(string connectionName) => new()
    {
        RecordType = PlatformCatalogRecordTypes.ExternalConnection,
        Operation = PlatformCatalogOperations.Upsert,
        RecordKey = connectionName,
        Owner = "platform",
        ChangeNote = "test change",
        VersionTag = "test-v1",
        ExternalConnection = new ExternalConnectionOptions
        {
            BaseUrl = "https://stock.test"
        }
    };

    private sealed class InMemoryCatalogStore : IPlatformCatalogMutationStore
    {
        public Dictionary<string, CapabilityDescriptor> Capabilities { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ExternalConnectionOptions> Connections { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, CatalogChangeRequestRecord> Changes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Versions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public CatalogChangeRequestRecord? LastMutationChange { get; private set; }

        public Task<IReadOnlyList<CapabilityDescriptor>> ListCapabilitiesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CapabilityDescriptor>>(Capabilities.Values.ToArray());

        public Task<IReadOnlyList<KeyValuePair<string, ExternalConnectionOptions>>> ListExternalConnectionsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<KeyValuePair<string, ExternalConnectionOptions>>>(Connections.ToArray());

        public Task<IReadOnlyList<CatalogChangeRequestRecord>> ListChangesAsync(string tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogChangeRequestRecord>>(Changes.Values.Where(c => c.TenantId == tenantId).ToArray());

        public Task<CatalogChangeRequestRecord?> GetChangeAsync(string tenantId, string changeId, CancellationToken ct) =>
            Task.FromResult(Changes.TryGetValue(changeId, out var change) && change.TenantId == tenantId ? change : null);

        public Task<CatalogRecordVersion> GetRecordVersionAsync(string recordType, string recordKey, CancellationToken ct)
        {
            var versionKey = $"{recordType}:{recordKey}";
            var exists = string.Equals(recordType, PlatformCatalogRecordTypes.Capability, StringComparison.OrdinalIgnoreCase)
                ? Capabilities.ContainsKey(recordKey)
                : Connections.ContainsKey(recordKey);

            return Task.FromResult(new CatalogRecordVersion
            {
                Exists = exists,
                VersionTag = Versions.GetValueOrDefault(versionKey, string.Empty)
            });
        }

        public Task<CatalogChangeRequestRecord?> FindDuplicatePendingChangeAsync(
            string tenantId,
            string recordType,
            string operation,
            string recordKey,
            string payloadHash,
            string idempotencyKey,
            CancellationToken ct)
        {
            var duplicate = Changes.Values.FirstOrDefault(change =>
                change.TenantId == tenantId
                && change.Status == PlatformCatalogChangeStatus.Pending
                && string.Equals(change.RecordType, recordType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(change.Operation, operation, StringComparison.OrdinalIgnoreCase)
                && string.Equals(change.RecordKey, recordKey, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(change.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(idempotencyKey)
                        && string.Equals(change.IdempotencyKey, idempotencyKey, StringComparison.OrdinalIgnoreCase))));

            return Task.FromResult(duplicate);
        }

        public Task<CatalogChangeRequestRecord> CreateChangeAsync(CatalogChangeRequestRecord change, CancellationToken ct)
        {
            Changes[change.ChangeId] = change;
            return Task.FromResult(change);
        }

        public Task<CatalogChangeRequestRecord> ApproveChangeAsync(string tenantId, string changeId, string reviewerUserId, CancellationToken ct)
        {
            var existing = Changes[changeId];
            var updated = existing.WithStatus(PlatformCatalogChangeStatus.Approved, reviewerUserId);
            Changes[changeId] = updated;
            return Task.FromResult(updated);
        }

        public Task<CatalogChangeRequestRecord> RejectChangeAsync(string tenantId, string changeId, string reviewerUserId, CancellationToken ct)
        {
            var existing = Changes[changeId];
            var updated = existing.WithStatus(PlatformCatalogChangeStatus.Rejected, reviewerUserId);
            Changes[changeId] = updated;
            return Task.FromResult(updated);
        }

        public Task<CatalogChangeRequestRecord> MarkAppliedAsync(string tenantId, string changeId, string appliedByUserId, CancellationToken ct)
        {
            var existing = Changes[changeId];
            var updated = new CatalogChangeRequestRecord
            {
                ChangeId = existing.ChangeId,
                TenantId = existing.TenantId,
                RecordType = existing.RecordType,
                Operation = existing.Operation,
                RecordKey = existing.RecordKey,
                PayloadJson = existing.PayloadJson,
                Status = PlatformCatalogChangeStatus.Applied,
                Owner = existing.Owner,
                ChangeNote = existing.ChangeNote,
                VersionTag = existing.VersionTag,
                ExpectedVersionTag = existing.ExpectedVersionTag,
                IdempotencyKey = existing.IdempotencyKey,
                RollbackOfChangeId = existing.RollbackOfChangeId,
                PayloadHash = existing.PayloadHash,
                RiskLevel = existing.RiskLevel,
                EnvironmentName = existing.EnvironmentName,
                BreakGlass = existing.BreakGlass,
                BreakGlassJustification = existing.BreakGlassJustification,
                RequestedByUserId = existing.RequestedByUserId,
                RequestedAtUtc = existing.RequestedAtUtc,
                ReviewedByUserId = existing.ReviewedByUserId,
                ReviewedAtUtc = existing.ReviewedAtUtc,
                AppliedByUserId = appliedByUserId,
                AppliedAtUtc = DateTime.UtcNow
            };
            Changes[changeId] = updated;
            return Task.FromResult(updated);
        }

        public Task UpsertCapabilityAsync(CapabilityDescriptor capability, CatalogChangeRequestRecord change, CancellationToken ct)
        {
            LastMutationChange = change;
            Capabilities[capability.CapabilityKey] = capability;
            Versions[$"capability:{capability.CapabilityKey}"] = change.VersionTag;
            return Task.CompletedTask;
        }

        public Task DisableCapabilityAsync(string capabilityKey, CatalogChangeRequestRecord change, CancellationToken ct)
        {
            LastMutationChange = change;
            Capabilities.Remove(capabilityKey);
            Versions.Remove($"capability:{capabilityKey}");
            return Task.CompletedTask;
        }

        public Task UpsertExternalConnectionAsync(string connectionName, ExternalConnectionOptions connection, CatalogChangeRequestRecord change, CancellationToken ct)
        {
            LastMutationChange = change;
            Connections[connectionName] = connection;
            Versions[$"external_connection:{connectionName}"] = change.VersionTag;
            return Task.CompletedTask;
        }

        public Task DisableExternalConnectionAsync(string connectionName, CatalogChangeRequestRecord change, CancellationToken ct)
        {
            LastMutationChange = change;
            Connections.Remove(connectionName);
            Versions.Remove($"external_connection:{connectionName}");
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingAuditLogger : IAuditLogger
    {
        public void LogAuthenticationEvent(AuthAuditEvent auditEvent) { }
        public void LogAuthorizationEvent(AuthzAuditEvent auditEvent) { }
        public void LogDataAccessEvent(DataAccessAuditEvent auditEvent) { }
        public void LogSecurityEvent(SecurityAuditEvent auditEvent) { }
        public void Log(AuditEvent auditEvent) { }
        public void LogGovernanceEvent(GovernanceAuditEvent @event) { }
    }

    private sealed class NoopMetricsService : IMetricsService
    {
        public void IncrementCounter(string name, Dictionary<string, string>? labels = null, double value = 1.0) { }
        public void RecordHistogram(string name, double value, Dictionary<string, string>? labels = null) { }
        public void RecordGauge(string name, double value, Dictionary<string, string>? labels = null) { }
        public IDisposable CreateTimer(string name, Dictionary<string, string>? labels = null) => new NoopTimer();
        private sealed class NoopTimer : IDisposable { public void Dispose() { } }
    }
}

file static class CatalogMutationRequestTestExtensions
{
    public static CatalogMutationRequest withExpectedVersion(
        this CatalogMutationRequest existing,
        string expectedVersionTag) => new()
    {
        RecordType = existing.RecordType,
        Operation = existing.Operation,
        RecordKey = existing.RecordKey,
        Capability = existing.Capability,
        ExternalConnection = existing.ExternalConnection,
        Owner = existing.Owner,
        ChangeNote = existing.ChangeNote,
        VersionTag = existing.VersionTag,
        ExpectedVersionTag = expectedVersionTag,
        IdempotencyKey = existing.IdempotencyKey,
        RollbackOfChangeId = existing.RollbackOfChangeId,
        BreakGlass = existing.BreakGlass,
        BreakGlassJustification = existing.BreakGlassJustification
    };

    public static CatalogMutationRequest withIdempotency(
        this CatalogMutationRequest existing,
        string idempotencyKey) => new()
    {
        RecordType = existing.RecordType,
        Operation = existing.Operation,
        RecordKey = existing.RecordKey,
        Capability = existing.Capability,
        ExternalConnection = existing.ExternalConnection,
        Owner = existing.Owner,
        ChangeNote = existing.ChangeNote,
        VersionTag = existing.VersionTag,
        ExpectedVersionTag = existing.ExpectedVersionTag,
        IdempotencyKey = idempotencyKey,
        RollbackOfChangeId = existing.RollbackOfChangeId,
        BreakGlass = existing.BreakGlass,
        BreakGlassJustification = existing.BreakGlassJustification
    };
}

file static class CatalogChangeTestExtensions
{
    public static CatalogChangeRequestRecord WithStatus(
        this CatalogChangeRequestRecord existing,
        string status,
        string reviewerUserId) => new()
    {
        ChangeId = existing.ChangeId,
        TenantId = existing.TenantId,
        RecordType = existing.RecordType,
        Operation = existing.Operation,
        RecordKey = existing.RecordKey,
        PayloadJson = existing.PayloadJson,
        Status = status,
        Owner = existing.Owner,
        ChangeNote = existing.ChangeNote,
        VersionTag = existing.VersionTag,
        ExpectedVersionTag = existing.ExpectedVersionTag,
        IdempotencyKey = existing.IdempotencyKey,
        RollbackOfChangeId = existing.RollbackOfChangeId,
        PayloadHash = existing.PayloadHash,
        RiskLevel = existing.RiskLevel,
        EnvironmentName = existing.EnvironmentName,
        BreakGlass = existing.BreakGlass,
        BreakGlassJustification = existing.BreakGlassJustification,
        RequestedByUserId = existing.RequestedByUserId,
        RequestedAtUtc = existing.RequestedAtUtc,
        ReviewedByUserId = reviewerUserId,
        ReviewedAtUtc = DateTime.UtcNow
    };
}
