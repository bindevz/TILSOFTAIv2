# Catalog Signed Evidence and Archives - Sprint 15

Sprint 15 promotes catalog release proof from provider-verified artifacts to signed evidence bundles and tamper-evident dossier archives.

## Signed Evidence

Evidence records can include:

- `signedPayload`: canonical release proof payload.
- `signature`: base64 RSA signature over `signedPayload`.
- `signatureAlgorithm`: `RS256`.
- `signerId`: configured release signer identity.
- `signerPublicKeyId`: configured public key id.

Verification uses configured `CatalogCertification:TrustedEvidenceSigners`. A passing RSA SHA-256 signature promotes evidence to `signature_verified`, records `VerificationMethod=signature`, and stamps `VerificationPolicyVersion`.

Invalid, unsupported, or untrusted signatures fail verification even when provider-backed artifact checks pass.

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

The archive service writes a JSON package under `CatalogCertification:DossierArchiveRootPath`. The archive record contains:

- manifest id,
- dossier hash,
- archive hash,
- archive path,
- policy version,
- retention deadline,
- creating user,
- creation timestamp.

The archive hash seals the manifest hash, dossier hash, policy version, signer metadata, and trusted evidence hashes. Any review package mismatch is surfaced by the dossier as `dossier_archive_hash_mismatch`.

## Production Completion

When `RequireArchivedDossierForProductionLikeCompletion=true`, production-like rollout completion blocks with `dossier_archive_required` until a dossier archive exists.

## Operator Flow

1. Record evidence with payload, signature, signer id, public key id, artifact hash, collection timestamp, source system, and evidence URI.
2. Verify evidence with `acceptAsTrusted=true`.
3. Issue the promotion manifest with trusted evidence ids.
4. Archive the dossier.
5. Record rollout completion with completion evidence.

Emergency paths do not bypass this proof chain. They must archive the review package before completion is accepted.
