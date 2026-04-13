# Catalog Signed Evidence and Archives - Sprint 16

Sprint 15 promotes catalog release proof from provider-verified artifacts to signed evidence bundles and tamper-evident dossier archives.
Sprint 16 operationalizes signer lifecycle and archive replay verification.

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

Supported operations:

- `add_signer_key`
- `rotate_signer_key`
- `revoke_signer_key`
- `retire_signer_key`

Production policy should keep `RequireIndependentSignerTrustApproval=true`, which prevents the requester from approving the same signer trust change.

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

The archive service writes a JSON package under `CatalogCertification:DossierArchiveRootPath`. The archive record contains:

- manifest id,
- dossier hash,
- archive hash,
- archive path,
- archive backend and storage URI,
- policy version,
- retention deadline,
- creating user,
- creation timestamp.

The archive hash seals the manifest hash, dossier hash, policy version, signer metadata, signer trust-store version, and trusted evidence hashes. Verification returns deterministic errors such as `archive_not_found`, `archive_json_invalid`, `archive_envelope_invalid`, `archive_hash_mismatch`, and `archive_manifest_mismatch`.

## Production Completion

When `RequireArchivedDossierForProductionLikeCompletion=true`, production-like rollout completion blocks with `dossier_archive_required` until a dossier archive exists.

## Operator Flow

1. Record evidence with payload, signature, signer id, public key id, artifact hash, collection timestamp, source system, and evidence URI.
2. Verify evidence with `acceptAsTrusted=true`.
3. Issue the promotion manifest with trusted evidence ids.
4. Archive the dossier.
5. Verify the archived dossier package.
6. Record rollout completion with completion evidence.

Emergency paths do not bypass this proof chain. They must archive the review package before completion is accepted.
