# Catalog Signed Evidence and Archives - Sprint 18

Sprint 15 promotes catalog release proof from provider-verified artifacts to signed evidence bundles and tamper-evident dossier archives.
Sprint 16 operationalizes signer lifecycle and archive replay verification.
Sprint 17 adds trust-store backup/restore verification and mirrored archive survivability.
Sprint 18 adds managed durable archive and trust-store recovery backends, backend-class policy enforcement, and explicit retention posture metadata.

## Signed Evidence

Evidence records can include:

- `signedPayload`: canonical release proof payload.
- `signature`: base64 RSA signature over `signedPayload`.
- `signatureAlgorithm`: `RS256`.
- `signerId`: configured release signer identity.
- `signerPublicKeyId`: configured public key id.

Verification uses the lifecycle-aware signer trust store. Bootstrap signers can still come from `CatalogCertification:TrustedEvidenceSigners`, but governed signer changes are stored in `CatalogCertification:SignerTrustStorePath`.

A passing RSA SHA-256 signature promotes evidence to `signature_verified`, records `VerificationMethod=signature`, and stamps `VerificationPolicyVersion`.

Invalid, unsupported, inactive, expired, retired, rotated, revoked, or untrusted signatures fail verification even when provider-backed artifact checks pass.

## Signer Lifecycle

Signer keys can be:

- `active`: usable for new signature verification.
- `rotated`: retained for historical interpretation but not usable for new verification.
- `revoked`: no longer trusted; historical releases emit review warnings when revocation happened after verification.
- `retired`: no longer used for new verification, retained for audit history.

Verification snapshots signer status, key id, key fingerprint, validity window, and trust-store version onto the evidence record so old releases remain interpretable after signer changes.

## Signer Trust Governance

Use:

- `GET /api/platform-catalog/signer-trust/signers`
- `GET /api/platform-catalog/signer-trust/changes`
- `POST /api/platform-catalog/signer-trust/changes`
- `POST /api/platform-catalog/signer-trust/changes/{changeId}/approve`
- `POST /api/platform-catalog/signer-trust/changes/{changeId}/reject`
- `POST /api/platform-catalog/signer-trust/changes/{changeId}/apply`
- `POST /api/platform-catalog/signer-trust/recovery/backup`
- `POST /api/platform-catalog/signer-trust/recovery/verify-backup`
- `POST /api/platform-catalog/signer-trust/recovery/restore-backup`

Supported operations:

- `add_signer_key`
- `rotate_signer_key`
- `revoke_signer_key`
- `retire_signer_key`

Production policy should keep `RequireIndependentSignerTrustApproval=true`, which prevents the requester from approving the same signer trust change.

## Trust-Store Recovery

`SignerTrustStorePath` remains the active governed trust store. Recovery storage is selected by `SignerTrustStoreBackupBackend`.

Supported recovery backends:

- `filesystem`: writes to `SignerTrustStoreBackupPath`, backend class `local_filesystem`, custody boundary `same_host_family`.
- `managed_sql`: writes to `dbo.PlatformCatalogSignerTrustBackup`, backend class `managed_durable`, custody boundary `database_managed`.

Recovery results include:

- source path,
- backup path,
- backup backend name,
- backup backend class,
- custody boundary,
- trust-store hash,
- expected hash,
- trust-store version,
- signer count,
- change count,
- deterministic errors.

Use the hash returned by backup as the expected hash when verifying or restoring. A mismatch returns `trust_store_backup_hash_mismatch`.

Production-like defaults require `MinimumTrustStoreDurabilityClassForProductionLike=managed_durable`.

## Policy Provenance

`CatalogCertification:PolicyVersion` is copied into:

- evidence verification results,
- evidence trust evaluations,
- dossier retention snapshots,
- archive records.

This makes every high-assurance release decision attributable to the exact policy version used at verification or archive time.

## Dossier Archives

Use:

`POST /api/platform-catalog/promotion-manifests/{manifestId}/dossier/archive`

Use:

`GET /api/platform-catalog/promotion-manifests/{manifestId}/dossier/archive/verify`

The archive service writes through the selected `DossierArchiveBackend`. The archive record contains:

- manifest id,
- dossier hash,
- archive hash,
- archive path,
- archive backend,
- backend durability class,
- retention posture,
- backend immutability flag,
- storage URI,
- recovery state,
- policy version,
- retention deadline,
- creating user,
- creation timestamp.

The archive hash seals the manifest hash, dossier hash, policy version, signer metadata, signer trust-store version, and trusted evidence hashes. Verification returns deterministic errors such as `archive_not_found`, `archive_json_invalid`, `archive_envelope_invalid`, `archive_hash_mismatch`, and `archive_manifest_mismatch`.

When `EnableDossierArchiveMirror=true`, archives are written to both `DossierArchiveRootPath` and `DossierArchiveMirrorRootPath`. Replay verification reads the primary archive first and falls back to the mirror with `RecoveryState=recovered_from_mirror`.

When `DossierArchiveBackend=managed_sql`, archives are stored in `dbo.PlatformCatalogDossierArchive`. A second write for the same manifest is accepted only when the stored archive hash matches, which gives the backend an explicit immutability check.

Backend classes:

- `local_filesystem`
- `mirrored_filesystem`
- `managed_durable`

Retention postures:

- `metadata_only`
- `retention_tracked`

## Production Completion

When `RequireArchivedDossierForProductionLikeCompletion=true`, production-like rollout completion blocks until the archive exists, replay-verifies, and satisfies configured backend policy.

Possible blockers:

- `dossier_archive_required`
- `dossier_archive_verification_failed:{error}`
- `dossier_archive_policy_failure:archive_durability_class_insufficient:{actual}:{required}`
- `dossier_archive_policy_failure:archive_retention_posture_insufficient:{actual}:{required}`

Recommended production-like defaults:

- `DossierArchiveBackend=managed_sql`
- `MinimumArchiveDurabilityClassForProductionLike=managed_durable`
- `RequiredArchiveRetentionPostureForProductionLike=retention_tracked`

## Operator Flow

1. Record evidence with payload, signature, signer id, public key id, artifact hash, collection timestamp, source system, and evidence URI.
2. Verify evidence with `acceptAsTrusted=true`.
3. Issue the promotion manifest with trusted evidence ids.
4. Archive the dossier.
5. Verify the archived dossier package.
6. Confirm archive backend class, retention posture, immutability flag, storage URI, and recovery state.
7. Backup the signer trust store after signer lifecycle changes.
8. Verify trust-store backup against the expected hash and confirm backend class plus custody boundary.
9. Record rollout completion with completion evidence.

Emergency paths do not bypass this proof chain. They must archive the review package before completion is accepted.
