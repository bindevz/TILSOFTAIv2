# Catalog Failure Drills - Sprint 12

These drills certify that operators can recover catalog mutation safely.

## Drill 1: Submit Validation Failure

Input: submit a capability update without `ArgumentContract`.

Expected:

- preview returns `isValid=false`,
- submit fails before creating a pending change,
- mutation failure metric increments,
- no SQL catalog record is changed.
- promotion gate returns `catalog_preview_failed` when the failed preview is evaluated.

Recovery:

- add the missing contract,
- rerun preview,
- submit with a fresh `IdempotencyKey`.

## Drill 2: Version Conflict

Input: submit an update for an existing record with stale `ExpectedVersionTag`.

Expected:

- preview returns `catalog_version_conflict`,
- submit fails before creating a pending change,
- apply rechecks version and blocks stale approved changes.
- promotion gate returns `catalog_expected_version_required` or `catalog_change_missing_expected_version` for production-like existing-record mutation without version coverage.

Recovery:

- reread the current record,
- update the payload against the current version,
- submit a new reviewed change.

## Drill 3: Duplicate Submit

Input: submit the same payload twice with the same `IdempotencyKey`.

Expected:

- second submit returns the existing pending change,
- only one pending change exists,
- the operator can continue with review/apply once.
- the evidence package records the duplicate replay as `duplicate_submit_drill` only after the replay is observed in the target environment.

Recovery:

- use the returned change id,
- do not create a second ticket for the same payload.

## Drill 4: SQL Unavailable During Apply

Input: approve a change, then simulate SQL outage before apply.

Expected:

- apply fails,
- change remains approved,
- no applied timestamp is written,
- retry is safe after SQL recovers.
- the approved change can still pass promotion gate evaluation after SQL recovers.

Recovery:

- restore SQL connectivity,
- rerun apply with the same change id,
- verify `AppliedAtUtc` and catalog record version.

## Drill 5: Production Fallback Risk

Input: production-like environment with bootstrap records present or platform catalog unavailable.

Expected:

- `/health/ready` reports unhealthy for `mixed` or `bootstrap_only` when strict production posture is enabled,
- startup logs `PlatformCatalogBootstrapFallbackProductionRisk`,
- deployment is blocked until platform mode is restored.
- promotion gate returns `production_mixed_source_mode_blocked` or `production_bootstrap_only_source_mode_blocked`.

Recovery:

- restore platform catalog records,
- remove bootstrap records from production config,
- keep `AllowBootstrapConfigurationFallback=false` for normal production deployments.

## Evidence Capture

After each drill completes in staging or another prod-like target, record one certification evidence item with:

- target `environmentName`,
- matching `evidenceKind`,
- `status=accepted` only after operator review,
- evidence URI or ticket link,
- related change or incident id when available.
- artifact hash, source system, content type, and collected timestamp.
- verification result before evidence is used for a promotion manifest.

Unit tests and local dry runs prove implementation behavior, but they do not satisfy live certification evidence.
