# Catalog Control Plane Runbook - Sprint 12

The catalog control plane is the production path for changing capability and external connection metadata.

## Operator Roles

| Role | Responsibility |
|------|----------------|
| `platform_catalog_admin` | Submit and preview catalog changes. |
| `platform_catalog_approver` | Approve standard-risk changes. |
| `platform_catalog_senior_approver` | Approve high-risk changes such as disables and external connection mutations. |
| `platform_catalog_operator` | Apply approved changes in production-like environments. |
| `platform_catalog_break_glass` | Reserved for emergency policy override when explicitly enabled. |

Production-like environments require two-person review and independent apply by default.

## Standard Change Flow

1. Read the current record and note its `VersionTag`.
2. Run `POST /api/platform-catalog/changes/preview` with the intended payload.
3. Confirm preview returns:
   - `isValid=true`,
   - expected `recordKey`,
   - expected `riskLevel`,
   - correct `currentVersionTag`,
   - no `duplicatePendingChangeId`.
4. Run `POST /api/platform-catalog/promotion-gate/evaluate` with the preview payload and target `environmentName`.
5. Confirm the gate returns:
   - `isAllowed=true`,
   - production-like source mode is `platform`,
   - no `blockers`,
   - no missing required evidence when `includeCertificationEvidence=true`.
6. Submit `POST /api/platform-catalog/changes` with:
   - `Owner`,
   - `ChangeNote`,
   - `VersionTag`,
   - `ExpectedVersionTag` for existing records,
   - `IdempotencyKey` from the change ticket.
7. Approver reviews and calls `POST /api/platform-catalog/changes/{changeId}/approve`.
8. Run `POST /api/platform-catalog/promotion-gate/evaluate` again with `changeId`.
9. Operator applies with `POST /api/platform-catalog/changes/{changeId}/apply`.
10. Verify `/health/ready` remains `platform` mode and the changed record resolves as expected.
11. Record certification evidence with `POST /api/platform-catalog/certification-evidence` when the runbook or drill was actually executed in the target environment.

## High-Risk Changes

High-risk changes include:

- disabling any catalog record,
- changing external connection records,
- break-glass changes.

High-risk changes require `platform_catalog_senior_approver` unless break-glass is explicitly enabled and audited.

Break-glass changes remain blocked by the promotion gate until after-action evidence is recorded and reviewed. Use the emergency path only for an incident, not for normal release timing.

## Retry Rules

- Re-submit with the same `IdempotencyKey` when a submit response is lost. The control plane returns the existing pending change when payload or key matches.
- Re-apply an already applied change safely. Apply is idempotent and returns the applied change.
- Do not change payloads while reusing an old idempotency key.

## Recovery And Rollback

Rollbacks are compensating catalog changes, not hidden rewrites.

1. Identify the bad applied change.
2. Create a new payload that restores the intended record state.
3. Set `RollbackOfChangeId` to the original change id.
4. Include the current `ExpectedVersionTag`.
5. Use the normal preview, submit, approve, and apply lifecycle.
6. Run the promotion gate before applying the compensating change.
7. Record rollback evidence and the original `RollbackOfChangeId`.

This preserves auditability and avoids unreviewed metadata rewinds.
