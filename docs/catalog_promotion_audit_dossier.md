# Catalog Promotion Audit Dossier - Sprint 15

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

## Audit Warnings

The dossier emits deterministic warnings when:

- manifest hash no longer matches immutable fields,
- a referenced change record is missing,
- a referenced evidence record is missing.
- referenced evidence is no longer trusted.
- retention windows have expired.
- a production-like dossier requires an archive but none exists.
- an existing archive no longer matches the current dossier hash.

Warnings are machine-readable and should block compliance sign-off until resolved.
