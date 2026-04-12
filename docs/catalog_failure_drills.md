# Catalog Failure Drills - Sprint 11

These drills certify that operators can recover catalog mutation safely.

## Drill 1: Submit Validation Failure

Input: submit a capability update without `ArgumentContract`.

Expected:

- preview returns `isValid=false`,
- submit fails before creating a pending change,
- mutation failure metric increments,
- no SQL catalog record is changed.

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

Recovery:

- restore platform catalog records,
- remove bootstrap records from production config,
- keep `AllowBootstrapConfigurationFallback=false` for normal production deployments.
