# Catalog Emergency Path Policy - Sprint 12

Emergency fallback and break-glass remain possible, but they are not normal operating paths.

## Bootstrap Fallback Re-Enable

Production fallback re-enable requires:

- incident id,
- platform owner approval,
- release manager approval,
- clear rollback plan,
- temporary configuration change with expiry,
- accepted after-action evidence.
- rollback or emergency promotion manifest when catalog state changes.
- signed evidence and an archived dossier before production-like completion.

Production-like `mixed` and `bootstrap_only` source modes block promotion by default.

## Break-Glass Catalog Mutation

Break-glass requires:

- `CatalogControlPlane:AllowBreakGlass=true`,
- caller has `platform_catalog_break_glass`,
- justification length meets policy,
- incident id in the change note or evidence,
- after-action evidence before subsequent promotion.
- immutable promotion manifest and rollout attestation for any catalog change.
- tamper-evident dossier archive for the emergency release package.

Break-glass does not bypass audit. It increases audit requirements.

## After-Action Requirements

Every fallback or break-glass incident must record:

- what failed,
- who authorized emergency action,
- what catalog records changed,
- rollback or compensating-change id,
- when durable platform source mode was restored,
- evidence URI and operator sign-off.
