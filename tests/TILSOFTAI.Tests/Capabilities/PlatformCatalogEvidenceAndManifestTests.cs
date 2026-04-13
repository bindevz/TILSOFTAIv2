using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Orchestration.Capabilities;
using Xunit;

namespace TILSOFTAI.Tests.Capabilities;

public sealed class PlatformCatalogEvidenceAndManifestTests
{
    [Fact]
    public void Verify_ShouldRejectUntrustedEvidenceReference()
    {
        var verifier = new PlatformCatalogEvidenceVerifier(Options.Create(CertificationOptions()), new StubArtifactProvider());
        var evidence = TrustedEvidence("ev-1") with { EvidenceUri = "https://untrusted.example/evidence.json" };

        var result = verifier.Verify(evidence, Context(), acceptAsTrusted: true, "verify");

        result.IsVerified.Should().BeFalse();
        result.Errors.Should().Contain("evidence_uri_not_allowed");
        verifier.IsTrusted(evidence, DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Verify_ShouldProviderVerifyControlledArtifactBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "tilsoftai-evidence-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var payload = "{\"proof\":\"ok\"}";
        var artifactPath = Path.Combine(root, "proof.json");
        File.WriteAllText(artifactPath, payload);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var options = CertificationOptions();
        options.TrustedArtifactRootPath = root;
        options.ControlledArtifactUriPrefixes = new[] { "artifact://catalog-evidence/" };
        options.AllowedEvidenceUriPrefixes = new[] { "artifact://catalog-evidence/" };
        var verifier = new PlatformCatalogEvidenceVerifier(
            Options.Create(options),
            new FileSystemCatalogArtifactProvider(Options.Create(options)));
        var evidence = TrustedEvidence("ev-1") with
        {
            EvidenceUri = "artifact://catalog-evidence/proof.json",
            ArtifactHash = hash,
            TrustTier = string.Empty,
            ArtifactProvider = string.Empty,
            ProviderVerifiedAtUtc = null,
            ArtifactSizeBytes = null
        };

        var result = verifier.Verify(evidence, Context(), acceptAsTrusted: true, "provider verify");

        result.IsVerified.Should().BeTrue();
        result.TrustTier.Should().Be(CatalogEvidenceTrustTiers.ProviderVerified);
        result.ArtifactProvider.Should().Be("filesystem");
        result.ArtifactSizeBytes.Should().Be(14);
    }

    [Fact]
    public void Verify_ShouldPromoteTrustedRsaSignature()
    {
        using var rsa = RSA.Create(2048);
        var payload = "{\"release\":\"sprint-15\"}";
        var options = CertificationOptions();
        options.TrustedEvidenceSigners = new[]
        {
            new CatalogTrustedSignerOptions
            {
                SignerId = "release-authority",
                KeyId = "key-1",
                PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem()
            }
        };
        var evidence = TrustedEvidence("ev-signed") with
        {
            SignedPayload = payload,
            Signature = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)),
            SignatureAlgorithm = "RS256",
            SignerId = "release-authority",
            SignerPublicKeyId = "key-1",
            TrustTier = string.Empty,
            VerificationMethod = string.Empty,
            VerificationPolicyVersion = string.Empty
        };
        var verifier = new PlatformCatalogEvidenceVerifier(
            Options.Create(options),
            new StubArtifactProvider(),
            new RsaPlatformCatalogSignatureVerifier(Options.Create(options)));

        var result = verifier.Verify(evidence, Context(), acceptAsTrusted: true, "signature verify");

        result.IsVerified.Should().BeTrue();
        result.TrustTier.Should().Be(CatalogEvidenceTrustTiers.SignatureVerified);
        result.VerificationMethod.Should().Be(CatalogEvidenceVerificationMethods.Signature);
        result.VerificationPolicyVersion.Should().Be("sprint-16");
        result.SignerStatusAtVerification.Should().Be(CatalogSignerLifecycleStates.Active);
        result.SignerPublicKeyFingerprint.Should().HaveLength(64);
        result.SignatureVerifiedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Verify_ShouldRejectRevokedSigner()
    {
        using var rsa = RSA.Create(2048);
        var payload = "{\"release\":\"sprint-16\"}";
        var options = CertificationOptions();
        options.TrustedEvidenceSigners = new[]
        {
            new CatalogTrustedSignerOptions
            {
                SignerId = "release-authority",
                KeyId = "key-1",
                PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem(),
                Status = CatalogSignerLifecycleStates.Revoked,
                RevokedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            }
        };
        var evidence = TrustedEvidence("ev-revoked") with
        {
            SignedPayload = payload,
            Signature = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)),
            SignatureAlgorithm = "RS256",
            SignerId = "release-authority",
            SignerPublicKeyId = "key-1"
        };
        var verifier = new PlatformCatalogEvidenceVerifier(
            Options.Create(options),
            new StubArtifactProvider(),
            new RsaPlatformCatalogSignatureVerifier(Options.Create(options)));

        var result = verifier.Verify(evidence, Context(), acceptAsTrusted: true, "signature verify");

        result.IsVerified.Should().BeFalse();
        result.Errors.Should().Contain(item => item.StartsWith("signature_signer_not_active", StringComparison.OrdinalIgnoreCase));
        result.Errors.Should().Contain("signature_signer_revoked");
    }

    [Fact]
    public void SignerTrustStore_ShouldGovernRotationThroughApproval()
    {
        using var original = RSA.Create(2048);
        using var rotated = RSA.Create(2048);
        var root = Path.Combine(Path.GetTempPath(), "tilsoftai-signer-tests", Guid.NewGuid().ToString("N"));
        var options = CertificationOptions();
        options.SignerTrustStorePath = Path.Combine(root, "trust-store.json");
        options.TrustedEvidenceSigners = new[]
        {
            new CatalogTrustedSignerOptions
            {
                SignerId = "release-authority",
                KeyId = "key-1",
                PublicKeyPem = original.ExportSubjectPublicKeyInfoPem()
            }
        };
        var store = new FileSystemPlatformCatalogSignerTrustStore(Options.Create(options));

        var proposed = store.ProposeChange(new CatalogSignerTrustMutationRequest
        {
            Operation = CatalogSignerTrustChangeOperations.RotateSignerKey,
            SignerId = "release-authority",
            KeyId = "key-2",
            RotatesFromKeyId = "key-1",
            PublicKeyPem = rotated.ExportSubjectPublicKeyInfoPem(),
            Reason = "scheduled signer rotation"
        }, Context());
        var selfApproval = store.ApproveChange(proposed.Change!.ChangeId, Context());
        var approved = store.ApproveChange(proposed.Change.ChangeId, Context(userId: "independent-approver"));
        var applied = store.ApplyChange(proposed.Change.ChangeId, Context(userId: "release-operator"));

        proposed.IsAccepted.Should().BeTrue();
        selfApproval.IsAccepted.Should().BeFalse();
        selfApproval.Blockers.Should().Contain("signer_trust_independent_approval_required");
        approved.IsAccepted.Should().BeTrue();
        applied.IsAccepted.Should().BeTrue();
        store.FindSigner("release-authority", "key-1")!.Status.Should().Be(CatalogSignerLifecycleStates.Rotated);
        store.FindSigner("release-authority", "key-2")!.Status.Should().Be(CatalogSignerLifecycleStates.Active);
        store.ListChanges().Should().ContainSingle(item => item.Status == CatalogSignerTrustChangeStatus.Applied);
    }

    [Fact]
    public void Verify_ShouldRejectInvalidRsaSignature()
    {
        using var rsa = RSA.Create(2048);
        var options = CertificationOptions();
        options.TrustedEvidenceSigners = new[]
        {
            new CatalogTrustedSignerOptions
            {
                SignerId = "release-authority",
                KeyId = "key-1",
                PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem()
            }
        };
        var evidence = TrustedEvidence("ev-signed") with
        {
            SignedPayload = "{\"release\":\"sprint-15\"}",
            Signature = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes("{\"release\":\"tampered\"}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)),
            SignatureAlgorithm = "RS256",
            SignerId = "release-authority",
            SignerPublicKeyId = "key-1"
        };
        var verifier = new PlatformCatalogEvidenceVerifier(
            Options.Create(options),
            new StubArtifactProvider(),
            new RsaPlatformCatalogSignatureVerifier(Options.Create(options)));

        var result = verifier.Verify(evidence, Context(), acceptAsTrusted: true, "signature verify");

        result.IsVerified.Should().BeFalse();
        result.Errors.Should().Contain("signature_invalid");
    }

    [Fact]
    public async Task IssueManifestAsync_ShouldBlock_WhenEvidenceIsNotTrusted()
    {
        var evidence = TrustedEvidence("ev-1") with { VerificationStatus = CatalogEvidenceVerificationStatus.Unverified };
        var service = CreateService(new[] { evidence });

        var result = await service.IssueManifestAsync(new CatalogPromotionManifestIssueRequest
        {
            EnvironmentName = "prod",
            ChangeIds = new[] { "chg-1" },
            EvidenceIds = new[] { "ev-1" }
        }, Context(), CancellationToken.None);

        result.IsIssued.Should().BeFalse();
        result.Blockers.Should().Contain("evidence_untrusted:ev-1");
    }

    [Fact]
    public async Task IssueManifestAsync_ShouldBlock_WhenTrustTierIsTooWeakForProduction()
    {
        var evidence = RequiredEvidence()
            .Select(item => item with { TrustTier = CatalogEvidenceTrustTiers.MetadataVerified })
            .ToArray();
        var service = CreateService(evidence);

        var result = await service.IssueManifestAsync(new CatalogPromotionManifestIssueRequest
        {
            EnvironmentName = "prod",
            ChangeIds = new[] { "chg-1" },
            EvidenceIds = evidence.Select(item => item.EvidenceId).ToArray()
        }, Context(), CancellationToken.None);

        result.IsIssued.Should().BeFalse();
        result.Blockers.Should().Contain(item => item.Contains("evidence_trust_tier_insufficient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IssueManifestAsync_ShouldBlock_WhenSignatureTierIsRequired()
    {
        var options = CertificationOptions();
        options.MinimumEvidenceTrustTierForProductionLikePromotion = CatalogEvidenceTrustTiers.SignatureVerified;
        options.EnvironmentMinimumEvidenceTrustTiers["prod"] = CatalogEvidenceTrustTiers.SignatureVerified;
        var evidence = RequiredEvidence();
        var service = CreateService(evidence, options: options);

        var result = await service.IssueManifestAsync(new CatalogPromotionManifestIssueRequest
        {
            EnvironmentName = "prod",
            ChangeIds = new[] { "chg-1" },
            EvidenceIds = evidence.Select(item => item.EvidenceId).ToArray()
        }, Context(), CancellationToken.None);

        result.IsIssued.Should().BeFalse();
        result.Blockers.Should().Contain(item => item.Contains("evidence_trust_tier_insufficient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IssueManifestAsync_ShouldBlock_WhenLiveCertificationEvidenceIsStale()
    {
        var evidence = RequiredEvidence()
            .Select(item => item with
            {
                CollectedAtUtc = DateTime.UtcNow.AddDays(-45),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
            })
            .ToArray();
        var service = CreateService(evidence);

        var result = await service.IssueManifestAsync(new CatalogPromotionManifestIssueRequest
        {
            EnvironmentName = "prod",
            ChangeIds = new[] { "chg-1" },
            EvidenceIds = evidence.Select(item => item.EvidenceId).ToArray()
        }, Context(), CancellationToken.None);

        result.IsIssued.Should().BeFalse();
        result.Blockers.Should().Contain(item => item.Contains("evidence_freshness_expired", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IssueManifestAsync_ShouldCreateManifest_WhenGateAndEvidenceAreTrusted()
    {
        var evidence = RequiredEvidence();
        var service = CreateService(evidence);

        var result = await service.IssueManifestAsync(new CatalogPromotionManifestIssueRequest
        {
            EnvironmentName = "prod",
            ChangeIds = new[] { "chg-1" },
            EvidenceIds = evidence.Select(item => item.EvidenceId).ToArray()
        }, Context(), CancellationToken.None);

        result.IsIssued.Should().BeTrue();
        result.Manifest.Should().NotBeNull();
        result.Manifest!.ManifestHash.Should().HaveLength(64);
        result.Manifest.ChangeIds.Should().Contain("chg-1");
    }

    [Fact]
    public async Task RecordAttestationAsync_ShouldRequireEvidenceForProductionCompletion()
    {
        var evidence = RequiredEvidence();
        var store = new StubManifestStore();
        var service = CreateService(evidence, store);
        var issue = await service.IssueManifestAsync(new CatalogPromotionManifestIssueRequest
        {
            EnvironmentName = "prod",
            ChangeIds = new[] { "chg-1" },
            EvidenceIds = evidence.Select(item => item.EvidenceId).ToArray()
        }, Context(), CancellationToken.None);

        var result = await service.RecordAttestationAsync(issue.Manifest!.ManifestId, new CatalogRolloutAttestationRequest
        {
            State = CatalogRolloutAttestationStates.Completed
        }, Context(), CancellationToken.None);

        result.IsRecorded.Should().BeFalse();
        result.Blockers.Should().Contain("rollout_completion_evidence_required");
    }

    [Fact]
    public async Task ArchiveDossierAsync_ShouldMaterializeTamperEvidentArchive()
    {
        var root = Path.Combine(Path.GetTempPath(), "tilsoftai-dossier-tests", Guid.NewGuid().ToString("N"));
        var options = CertificationOptions();
        options.DossierArchiveRootPath = root;
        var evidence = RequiredEvidence();
        var store = new StubManifestStore();
        var service = CreateService(evidence, store, options);
        var issue = await service.IssueManifestAsync(new CatalogPromotionManifestIssueRequest
        {
            EnvironmentName = "prod",
            ChangeIds = new[] { "chg-1" },
            EvidenceIds = evidence.Select(item => item.EvidenceId).ToArray()
        }, Context(), CancellationToken.None);

        var archive = await service.ArchiveDossierAsync(issue.Manifest!.ManifestId, Context(), CancellationToken.None);
        var dossier = await service.GetDossierAsync(issue.Manifest.ManifestId, Context(), CancellationToken.None);

        archive.IsArchived.Should().BeTrue();
        archive.Archive.Should().NotBeNull();
        archive.Archive!.ArchiveHash.Should().HaveLength(64);
        File.Exists(archive.Archive.ArchivePath).Should().BeTrue();
        dossier!.Archive.Should().NotBeNull();
        dossier.ArchiveVerification!.IsVerified.Should().BeTrue();
        dossier.AuditWarnings.Should().NotContain("dossier_archive_required");
    }

    [Fact]
    public async Task VerifyDossierArchiveAsync_ShouldDetectArchiveTampering()
    {
        var root = Path.Combine(Path.GetTempPath(), "tilsoftai-dossier-tests", Guid.NewGuid().ToString("N"));
        var options = CertificationOptions();
        options.DossierArchiveRootPath = root;
        var evidence = RequiredEvidence();
        var store = new StubManifestStore();
        var service = CreateService(evidence, store, options);
        var issue = await service.IssueManifestAsync(new CatalogPromotionManifestIssueRequest
        {
            EnvironmentName = "prod",
            ChangeIds = new[] { "chg-1" },
            EvidenceIds = evidence.Select(item => item.EvidenceId).ToArray()
        }, Context(), CancellationToken.None);
        var archive = await service.ArchiveDossierAsync(issue.Manifest!.ManifestId, Context(), CancellationToken.None);
        File.AppendAllText(archive.Archive!.ArchivePath, "tampered");

        var verification = await service.VerifyDossierArchiveAsync(issue.Manifest.ManifestId, Context(), CancellationToken.None);

        verification.IsVerified.Should().BeFalse();
        verification.Errors.Should().Contain("archive_json_invalid");
    }

    private static PlatformCatalogPromotionManifestService CreateService(
        IReadOnlyList<CatalogCertificationEvidenceRecord> evidence,
        StubManifestStore? manifestStore = null,
        CatalogCertificationOptions? options = null)
    {
        options ??= CertificationOptions();
        return
        new(
            new StubPromotionGate(),
            new StubCertificationStore(evidence),
            new StubMutationStore(),
            manifestStore ?? new StubManifestStore(),
            new PlatformCatalogEvidenceVerifier(Options.Create(options), new StubArtifactProvider()),
            Options.Create(options));
    }

    private static CatalogCertificationOptions CertificationOptions() => new()
    {
        EnvironmentName = "prod",
        RequiredEvidenceKinds = new[]
        {
            CatalogCertificationEvidenceKinds.RunbookExecution,
            CatalogCertificationEvidenceKinds.OperatorSignoff
        },
        AllowedEvidenceUriPrefixes = new[] { "https://evidence.example/" },
        TrustedEvidenceStatuses = new[] { CatalogCertificationEvidenceStatus.Accepted },
        MinimumEvidenceTrustTierForProductionLikePromotion = CatalogEvidenceTrustTiers.ProviderVerified,
        EvidenceFreshnessDaysByKind = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [CatalogCertificationEvidenceKinds.RunbookExecution] = 30,
            [CatalogCertificationEvidenceKinds.OperatorSignoff] = 14
        }
    };

    private static IReadOnlyList<CatalogCertificationEvidenceRecord> RequiredEvidence() => new[]
    {
        TrustedEvidence("ev-runbook", CatalogCertificationEvidenceKinds.RunbookExecution),
        TrustedEvidence("ev-signoff", CatalogCertificationEvidenceKinds.OperatorSignoff)
    };

    private static CatalogCertificationEvidenceRecord TrustedEvidence(
        string evidenceId,
        string evidenceKind = CatalogCertificationEvidenceKinds.RunbookExecution) => new()
    {
        EvidenceId = evidenceId,
        EnvironmentName = "prod",
        EvidenceKind = evidenceKind,
        Status = CatalogCertificationEvidenceStatus.Accepted,
        Summary = $"{evidenceKind} accepted",
        EvidenceUri = $"https://evidence.example/{evidenceId}.json",
        OperatorUserId = "operator",
        ArtifactHash = new string('b', 64),
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
    };

    private static CatalogMutationContext Context(string userId = "release-authority") => new()
    {
        TenantId = "tenant-1",
        UserId = userId,
        Roles = new[] { "platform_catalog_admin" },
        CorrelationId = "corr-1"
    };

    private sealed class StubPromotionGate : IPlatformCatalogPromotionGate
    {
        public Task<CatalogPromotionGateResult> EvaluateAsync(CatalogPromotionGateRequest request, CatalogMutationContext context, CancellationToken ct) =>
            Task.FromResult(new CatalogPromotionGateResult
            {
                IsAllowed = true,
                EnvironmentName = request.EnvironmentName,
                ChangeId = request.ChangeId,
                SourceMode = "platform",
                ProductionLike = true
            });

        public IReadOnlyList<CatalogControlPlaneSloDefinition> GetSloDefinitions() => Array.Empty<CatalogControlPlaneSloDefinition>();
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

    private sealed class StubCertificationStore : IPlatformCatalogCertificationStore
    {
        private readonly Dictionary<string, CatalogCertificationEvidenceRecord> _records;

        public StubCertificationStore(IReadOnlyList<CatalogCertificationEvidenceRecord> records)
        {
            _records = records.ToDictionary(item => item.EvidenceId, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyList<CatalogCertificationEvidenceRecord>> ListEvidenceAsync(string environmentName, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogCertificationEvidenceRecord>>(_records.Values.ToArray());

        public Task<CatalogCertificationEvidenceRecord?> GetEvidenceAsync(string evidenceId, CancellationToken ct) =>
            Task.FromResult(_records.TryGetValue(evidenceId, out var record) ? record : null);

        public Task<CatalogCertificationEvidenceRecord> CreateEvidenceAsync(CatalogCertificationEvidenceRecord evidence, CancellationToken ct) =>
            Task.FromResult(evidence);

        public Task<CatalogCertificationEvidenceRecord> UpdateEvidenceVerificationAsync(string evidenceId, CatalogEvidenceVerificationResult result, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class StubManifestStore : IPlatformCatalogPromotionManifestStore
    {
        private readonly Dictionary<string, CatalogPromotionManifestRecord> _manifests = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<CatalogRolloutAttestationRecord> _attestations = new();

        public Task<CatalogPromotionManifestRecord> CreateManifestAsync(CatalogPromotionManifestRecord manifest, CancellationToken ct)
        {
            _manifests[manifest.ManifestId] = manifest;
            return Task.FromResult(manifest);
        }

        public Task<CatalogPromotionManifestRecord?> GetManifestAsync(string manifestId, CancellationToken ct) =>
            Task.FromResult(_manifests.TryGetValue(manifestId, out var manifest) ? manifest : null);

        public Task<IReadOnlyList<CatalogPromotionManifestRecord>> ListManifestsAsync(string environmentName, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogPromotionManifestRecord>>(_manifests.Values.ToArray());

        public Task<CatalogRolloutAttestationRecord> CreateAttestationAsync(CatalogRolloutAttestationRecord attestation, CancellationToken ct)
        {
            _attestations.Add(attestation);
            return Task.FromResult(attestation);
        }

        public Task<IReadOnlyList<CatalogRolloutAttestationRecord>> ListAttestationsAsync(string manifestId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogRolloutAttestationRecord>>(_attestations.Where(item => item.ManifestId == manifestId).ToArray());
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
            Task.FromResult<CatalogChangeRequestRecord?>(new CatalogChangeRequestRecord
            {
                TenantId = tenantId,
                ChangeId = changeId,
                Status = PlatformCatalogChangeStatus.Approved,
                ExpectedVersionTag = "v1"
            });

        public Task<CatalogRecordVersion> GetRecordVersionAsync(string recordType, string recordKey, CancellationToken ct) =>
            Task.FromResult(new CatalogRecordVersion());

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
}
