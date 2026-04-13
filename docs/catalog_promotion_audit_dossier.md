# Catalog Promotion Audit Dossier - Sprint 13

The promotion dossier is the compliance review surface for catalog rollout history.

Use `GET /api/platform-catalog/promotion-manifests/{manifestId}/dossier`.

## Dossier Contents

The dossier includes:

- immutable promotion manifest,
- referenced catalog change records,
- trusted evidence records,
- rollout attestations,
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

## Audit Warnings

The dossier emits deterministic warnings when:

- manifest hash no longer matches immutable fields,
- a referenced change record is missing,
- a referenced evidence record is missing.

Warnings are machine-readable and should block compliance sign-off until resolved.
