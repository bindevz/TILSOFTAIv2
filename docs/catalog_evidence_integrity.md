# Catalog Evidence Integrity - Sprint 14

Sprint 13 upgraded certification evidence from operator metadata to a policy-bearing trust object. Sprint 14 adds provider-backed artifact verification and trust tiers.

## Evidence Lifecycle

Evidence status can now represent:

- `recorded`: captured but not trusted.
- `verified`: metadata and reference checks passed.
- `accepted`: verified and accepted as trusted by a release authority.
- `expired`: no longer valid for promotion.
- `superseded`: replaced by newer evidence.
- `rejected`: explicitly not usable.

Production-like promotion requires trusted evidence by default. Trusted evidence must have `VerificationStatus=verified`, a trusted lifecycle status, sufficient trust tier for the target environment, a valid evidence reference, a valid artifact hash, and a non-stale collection timestamp.

## Verification Rules

Evidence verification checks:

- evidence URI is present when required,
- evidence URI starts with an allowed configured prefix,
- artifact hash is present when required,
- artifact hash is SHA-256 hex,
- controlled artifact provider hash matches artifact bytes when the evidence URI is provider-backed,
- artifact hash algorithm is `sha256`,
- content type and source system are allowed when provided,
- collected timestamp exists, is not in the future, and is not stale.

The verifier does not fetch arbitrary external links. It verifies policy metadata and allowed reference shape, and it can verify bytes only through controlled providers such as the filesystem-backed `artifact://catalog-evidence/` provider.

## API Flow

1. Record evidence with `POST /api/platform-catalog/certification-evidence`.
2. Include artifact hash, content type, artifact type, source system, collection timestamp, and evidence URI.
3. Verify evidence with `POST /api/platform-catalog/certification-evidence/{evidenceId}/verify`.
4. Use `acceptAsTrusted=true` only when the verifier/release authority accepts the artifact for promotion.

Recorded evidence is audit data. Accepted verified evidence is promotion evidence.
