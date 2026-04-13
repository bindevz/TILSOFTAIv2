# Catalog Emergency Path Policy - Sprint 18

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
- archive replay verification before emergency completion sign-off.
- trust-store backup verification when emergency signer changes occur.
- archive backend class and retention posture must meet production-like policy unless the incident record explicitly documents a temporary downgrade.
- trust-store recovery backend class must meet production-like policy after emergency signer changes.

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
- review of signer lifecycle state if any emergency evidence signer was rotated or revoked after verification.
- archive mirror recovery check when the primary archive path was restored or rebuilt.
- managed durable archive replay verification when production-like policy requires `managed_durable`.
- managed durable trust-store backup verification when emergency signer lifecycle state changed.

Break-glass does not bypass audit. It increases audit requirements.

## After-Action Requirements

Every fallback or break-glass incident must record:

- what failed,
- who authorized emergency action,
- what catalog records changed,
- rollback or compensating-change id,
- when durable platform source mode was restored,
- evidence URI and operator sign-off.
- archive backend class, retention posture, immutability flag, and recovery state.
- signer trust-store recovery backend class and custody boundary when signer state changed.
