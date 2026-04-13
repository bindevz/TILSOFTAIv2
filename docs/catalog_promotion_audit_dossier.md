# Catalog Promotion Audit Dossier - Sprint 18

The promotion dossier is the compliance review surface for catalog rollout history.

Use `GET /api/platform-catalog/promotion-manifests/{manifestId}/dossier`.
Use `POST /api/platform-catalog/promotion-manifests/{manifestId}/dossier/archive` to materialize the tamper-evident archive package.

## Dossier Contents

The dossier includes:

- immutable promotion manifest,
- referenced catalog change records,
- trusted evidence records,
- evidence trust evaluations,
- rollout attestations,
- retention and archive policy snapshot,
- archive metadata when materialized,
- archive verification outcome when available,
- archive backend, backend class, retention posture, immutability flag, storage URI, and recovery state,
- deterministic dossier hash,
- audit warnings,
- generation timestamp.

## Review Questions

The dossier must answer:

- what changed,
- why it changed,
- who approved and issued promotion,
- which evidence was trusted,
- where the change was promoted,
- whether rollout completed, failed, aborted, or was superseded,
- whether rollback lineage exists.
- whether evidence trust tier and freshness still satisfy policy.
- whether retention windows remain current.
- whether archive backend class and retention posture satisfy production-like policy.

## Audit Warnings

The dossier emits deterministic warnings when:

- manifest hash no longer matches immutable fields,
- a referenced change record is missing,
- a referenced evidence record is missing.
- referenced evidence is no longer trusted.
- retention windows have expired.
- a production-like dossier requires an archive but none exists.
- an existing archive no longer matches the current dossier hash.
- archive replay verification fails.
- archive backend durability class is weaker than production-like policy.
- archive retention posture is weaker than production-like policy.
- signer lifecycle changed after evidence verification.
- replay verification recovered from a mirror after primary archive loss.

Warnings are machine-readable and should block compliance sign-off until resolved.
