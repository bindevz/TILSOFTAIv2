using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Orchestration.Capabilities;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class PlatformCatalogPromotionGateTests
{
    [Fact]
    public async Task EvaluateAsync_ShouldBlockMixedSourceModeInProduction()
    {
        var gate = CreateGate(
            bootstrapRecords: true,
            evidence: RequiredEvidence("prod"));

        var result = await gate.EvaluateAsync(
            new CatalogPromotionGateRequest { EnvironmentName = "prod", IncludeCertificationEvidence = true },
            Context(),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.Blockers.Should().Contain("production_mixed_source_mode_blocked");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldBlockProductionPromotion_WhenEvidenceIsMissing()
    {
        var gate = CreateGate(evidence: Array.Empty<CatalogCertificationEvidenceRecord>());

        var result = await gate.EvaluateAsync(
            new CatalogPromotionGateRequest { EnvironmentName = "prod", IncludeCertificationEvidence = true },
            Context(),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.Blockers.Should().Contain("catalog_certification_evidence_missing");
        result.EvidenceMissing.Should().Contain(CatalogCertificationEvidenceKinds.RunbookExecution);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldBlockProductionPromotion_WhenEvidenceIsAcceptedButUnverified()
    {
        var unverifiedEvidence = RequiredEvidence("prod")
            .Select(item => item with
            {
                VerificationStatus = CatalogEvidenceVerificationStatus.Unverified,
                ArtifactHash = string.Empty
            })
            .ToArray();
        var gate = CreateGate(evidence: unverifiedEvidence);

        var result = await gate.EvaluateAsync(
            new CatalogPromotionGateRequest { EnvironmentName = "prod", IncludeCertificationEvidence = true },
            Context(),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.Blockers.Should().Contain("catalog_certification_evidence_missing");
        result.EvidenceUntrusted.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_ShouldBlock_WhenPreviewFails()
    {
        var gate = CreateGate(
            previewValid: false,
            evidence: RequiredEvidence("prod"));

        var result = await gate.EvaluateAsync(
            new CatalogPromotionGateRequest
            {
                EnvironmentName = "prod",
                IncludeCertificationEvidence = true,
                MutationPreview = Mutation()
            },
            Context(),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.Blockers.Should().Contain("catalog_preview_failed");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldAllowProductionPromotion_WhenSourcePreviewAndEvidenceAreSafe()
    {
        var gate = CreateGate(evidence: RequiredEvidence("prod"));

        var result = await gate.EvaluateAsync(
            new CatalogPromotionGateRequest
            {
                EnvironmentName = "prod",
                IncludeCertificationEvidence = true,
                MutationPreview = Mutation(expectedVersionTag: "v1")
            },
            Context(),
            CancellationToken.None);

        result.IsAllowed.Should().BeTrue();
        result.SourceMode.Should().Be("platform");
    }

    private static PlatformCatalogPromotionGate CreateGate(
        bool bootstrapRecords = false,
        bool previewValid = true,
        IReadOnlyList<CatalogCertificationEvidenceRecord>? evidence = null)
    {
        var provider = new StubCatalogProvider();
        var controlPlane = new StubControlPlane(previewValid);
        var mutationStore = new StubMutationStore();
        var certificationStore = new StubCertificationStore(evidence ?? RequiredEvidence("prod"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(bootstrapRecords
                ? new Dictionary<string, string?>
                {
                    ["Capabilities:0:CapabilityKey"] = "bootstrap.capability"
                }
                : new Dictionary<string, string?>())
            .Build();

        return new PlatformCatalogPromotionGate(
            provider,
            controlPlane,
            mutationStore,
            certificationStore,
            new PlatformCatalogEvidenceVerifier(
                Options.Create(new CatalogCertificationOptions { EnvironmentName = "prod" }),
                new StubArtifactProvider()),
            configuration,
            Options.Create(new PlatformCatalogOptions
            {
                EnvironmentName = "prod",
                AllowBootstrapConfigurationFallback = true,
                TreatMixedAsUnhealthyInProductionLike = true
            }),
            Options.Create(new CatalogControlPlaneOptions { EnvironmentName = "prod" }),
            Options.Create(new CatalogCertificationOptions { EnvironmentName = "prod" }),
            new NoopMetricsService());
    }

    private static CatalogMutationContext Context() => new()
    {
        TenantId = "tenant-1",
        UserId = "operator-1",
        Roles = new[] { "platform_catalog_admin" },
        CorrelationId = "corr-1"
    };

    private static CatalogMutationRequest Mutation(string expectedVersionTag = "") => new()
    {
        RecordType = PlatformCatalogRecordTypes.Capability,
        Operation = PlatformCatalogOperations.Upsert,
        RecordKey = "warehouse.inventory.summary",
        ExpectedVersionTag = expectedVersionTag,
        Owner = "platform",
        ChangeNote = "test",
        VersionTag = "v2",
        Capability = new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.inventory.summary",
            Domain = "warehouse",
            AdapterType = "sql",
            Operation = "execute_query",
            TargetSystemId = "sql",
            ArgumentContract = new CapabilityArgumentContract { AllowAdditionalArguments = false }
        }
    };

    private static IReadOnlyList<CatalogCertificationEvidenceRecord> RequiredEvidence(string environment) =>
        new CatalogCertificationOptions().RequiredEvidenceKinds
            .Select(kind => new CatalogCertificationEvidenceRecord
            {
                EvidenceId = Guid.NewGuid().ToString("N"),
                EnvironmentName = environment,
                EvidenceKind = kind,
                Status = CatalogCertificationEvidenceStatus.Accepted,
                Summary = $"{kind} accepted",
                EvidenceUri = "https://evidence.example/CHG-123",
                OperatorUserId = "operator",
                ArtifactHash = new string('a', 64),
                ArtifactHashAlgorithm = "sha256",
                ArtifactContentType = "application/json",
                ArtifactType = "runbook",
                SourceSystem = "runbook",
                CollectedAtUtc = DateTime.UtcNow,
                VerificationStatus = CatalogEvidenceVerificationStatus.Verified,
                TrustTier = CatalogEvidenceTrustTiers.ProviderVerified,
                ArtifactProvider = "test-provider",
                ProviderVerifiedAtUtc = DateTime.UtcNow,
                ArtifactSizeBytes = 128,
                VerifiedByUserId = "verifier",
                VerifiedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
            })
            .ToArray();

    private sealed class StubCatalogProvider : IPlatformCatalogProvider
    {
        public PlatformCatalogSnapshot Load() => new()
        {
            CatalogFound = true,
            IsValid = true,
            Capabilities = new[]
            {
                new CapabilityDescriptor
                {
                    CapabilityKey = "warehouse.inventory.summary",
                    Domain = "warehouse",
                    AdapterType = "sql",
                    Operation = "execute_query",
                    TargetSystemId = "sql",
                    ArgumentContract = new CapabilityArgumentContract { AllowAdditionalArguments = false }
                }
            },
            ExternalConnections = new Dictionary<string, ExternalConnectionOptions>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private sealed class StubArtifactProvider : IPlatformCatalogArtifactProvider
    {
        public CatalogArtifactVerificationResult Verify(CatalogCertificationEvidenceRecord evidence) => new()
        {
            WasProviderControlled = true,
            IsVerified = true,
            ProviderName = "test-provider",
            ComputedSha256 = evidence.ArtifactHash,
            ArtifactSizeBytes = 128
        };
    }

    private sealed class StubControlPlane : IPlatformCatalogControlPlane
    {
        private readonly bool _previewValid;

        public StubControlPlane(bool previewValid)
        {
            _previewValid = previewValid;
        }

        public Task<IReadOnlyList<CapabilityDescriptor>> ListCapabilitiesAsync(CatalogMutationContext context, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CapabilityDescriptor>>(Array.Empty<CapabilityDescriptor>());

        public Task<IReadOnlyList<KeyValuePair<string, ExternalConnectionOptions>>> ListExternalConnectionsAsync(CatalogMutationContext context, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<KeyValuePair<string, ExternalConnectionOptions>>>(Array.Empty<KeyValuePair<string, ExternalConnectionOptions>>());

        public Task<IReadOnlyList<CatalogChangeRequestRecord>> ListChangesAsync(CatalogMutationContext context, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogChangeRequestRecord>>(Array.Empty<CatalogChangeRequestRecord>());

        public Task<CatalogMutationPreviewResult> PreviewAsync(CatalogMutationRequest request, CatalogMutationContext context, CancellationToken ct) =>
            Task.FromResult(new CatalogMutationPreviewResult
            {
                IsValid = _previewValid,
                RecordType = request.RecordType,
                Operation = request.Operation,
                RecordKey = request.RecordKey,
                ExistingRecordFound = true,
                CurrentVersionTag = "v1",
                ExpectedVersionTag = request.ExpectedVersionTag,
                Errors = _previewValid ? Array.Empty<string>() : new[] { "preview_failed" }
            });

        public Task<CatalogChangeRequestRecord> ProposeAsync(CatalogMutationRequest request, CatalogMutationContext context, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CatalogChangeRequestRecord> ApproveAsync(string changeId, CatalogMutationContext context, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CatalogChangeRequestRecord> RejectAsync(string changeId, CatalogMutationContext context, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CatalogChangeRequestRecord> ApplyAsync(string changeId, CatalogMutationContext context, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class StubMutationStore : IPlatformCatalogMutationStore
    {
        public Task<IReadOnlyList<CapabilityDescriptor>> ListCapabilitiesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CapabilityDescriptor>>(Array.Empty<CapabilityDescriptor>());

        public Task<IReadOnlyList<KeyValuePair<string, ExternalConnectionOptions>>> ListExternalConnectionsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<KeyValuePair<string, ExternalConnectionOptions>>>(Array.Empty<KeyValuePair<string, ExternalConnectionOptions>>());

        public Task<IReadOnlyList<CatalogChangeRequestRecord>> ListChangesAsync(string tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogChangeRequestRecord>>(Array.Empty<CatalogChangeRequestRecord>());

        public Task<CatalogChangeRequestRecord?> GetChangeAsync(string tenantId, string changeId, CancellationToken ct) =>
            Task.FromResult<CatalogChangeRequestRecord?>(null);

        public Task<CatalogRecordVersion> GetRecordVersionAsync(string recordType, string recordKey, CancellationToken ct) =>
            Task.FromResult(new CatalogRecordVersion { Exists = true, VersionTag = "v1" });

        public Task<CatalogChangeRequestRecord?> FindDuplicatePendingChangeAsync(string tenantId, string recordType, string operation, string recordKey, string payloadHash, string idempotencyKey, CancellationToken ct) =>
            Task.FromResult<CatalogChangeRequestRecord?>(null);

        public Task<CatalogChangeRequestRecord> CreateChangeAsync(CatalogChangeRequestRecord change, CancellationToken ct) =>
            Task.FromResult(change);

        public Task<CatalogChangeRequestRecord> ApproveChangeAsync(string tenantId, string changeId, string reviewerUserId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CatalogChangeRequestRecord> RejectChangeAsync(string tenantId, string changeId, string reviewerUserId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CatalogChangeRequestRecord> MarkAppliedAsync(string tenantId, string changeId, string appliedByUserId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task UpsertCapabilityAsync(CapabilityDescriptor capability, CatalogChangeRequestRecord change, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task DisableCapabilityAsync(string capabilityKey, CatalogChangeRequestRecord change, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task UpsertExternalConnectionAsync(string connectionName, ExternalConnectionOptions connection, CatalogChangeRequestRecord change, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task DisableExternalConnectionAsync(string connectionName, CatalogChangeRequestRecord change, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class StubCertificationStore : IPlatformCatalogCertificationStore
    {
        private readonly IReadOnlyList<CatalogCertificationEvidenceRecord> _evidence;

        public StubCertificationStore(IReadOnlyList<CatalogCertificationEvidenceRecord> evidence)
        {
            _evidence = evidence;
        }

        public Task<IReadOnlyList<CatalogCertificationEvidenceRecord>> ListEvidenceAsync(string environmentName, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogCertificationEvidenceRecord>>(_evidence);

        public Task<CatalogCertificationEvidenceRecord?> GetEvidenceAsync(string evidenceId, CancellationToken ct) =>
            Task.FromResult(_evidence.FirstOrDefault(item => string.Equals(item.EvidenceId, evidenceId, StringComparison.OrdinalIgnoreCase)));

        public Task<CatalogCertificationEvidenceRecord> CreateEvidenceAsync(CatalogCertificationEvidenceRecord evidence, CancellationToken ct) =>
            Task.FromResult(evidence);

        public Task<CatalogCertificationEvidenceRecord> UpdateEvidenceVerificationAsync(string evidenceId, CatalogEvidenceVerificationResult result, CancellationToken ct) =>
            throw new NotSupportedException();
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
