# Catalog Artifact Trust And Retention - Sprint 14

Sprint 14 adds a stronger trust path for certification evidence and durable audit proof.

## Trust Tiers

Evidence trust tiers are:

- `metadata_verified`: evidence metadata passed policy checks.
- `provider_verified`: a controlled provider read artifact bytes and recomputed the declared SHA-256 hash.
- `signature_verified`: reserved for signed bundle verification.
- `compliance_grade_trusted`: reserved for policy that combines provider/signature verification with archive controls.

Production-like environments require `provider_verified` evidence by default. Lower environments can require weaker tiers through `CatalogCertification:EnvironmentMinimumEvidenceTrustTiers`.

## Controlled Artifact Provider

The built-in provider verifies `artifact://catalog-evidence/...` references against `CatalogCertification:TrustedArtifactRootPath`.

It:

- rejects path traversal,
- refuses missing files,
- reads bytes only from the controlled root,
- recomputes SHA-256,
- compares the computed hash to `ArtifactHash`,
- records provider, byte count, and provider verification time.

It does not fetch arbitrary external URLs.

## Freshness

`CatalogCertification:EvidenceFreshnessDaysByKind` defines per-evidence-kind freshness windows. Required evidence that is too old blocks promotion and manifest issuance with deterministic trust-failure blockers.

## Retention

Retention policy is surfaced in promotion dossiers:

- evidence retention,
- manifest retention,
- rollout attestation retention,
- dossier archive retention,
- whether archive is required for production-like environments.

Dossiers warn when retention windows have expired. This makes retention policy executable review data instead of prose only.
