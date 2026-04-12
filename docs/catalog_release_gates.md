# Catalog Release Gates - Sprint 12

Catalog changes must pass promotion gates before production-like rollout.

## Gate Inputs

`POST /api/platform-catalog/promotion-gate/evaluate` accepts:

- `environmentName`
- optional `changeId`
- optional `mutationPreview`
- `includeCertificationEvidence`

Use `mutationPreview` for pre-submit release checks and `changeId` for approved-change promotion checks.

## Blocking Conditions

Promotion is blocked when any condition is true:

- platform catalog integrity is invalid,
- source mode is `empty`,
- production-like source mode is `mixed`,
- production-like source mode is `bootstrap_only`,
- mutation preview fails,
- existing production-like record mutation lacks `ExpectedVersionTag`,
- change id is missing or not found,
- change is neither approved nor already applied,
- existing production-like change lacks expected version,
- break-glass change lacks after-action evidence,
- required certification evidence is missing.

## CI/CD Usage

1. Run preview gate with the proposed payload.
2. Submit only if `isAllowed=true`.
3. After approval, run change gate with `changeId`.
4. Apply only if `isAllowed=true`.
5. Record resulting evidence for staging/prod-like certification.

The gate returns deterministic `blockers`, `warnings`, and `evidenceMissing` arrays for automation and operator review.

## Human Override

Do not bypass the gate for convenience. Emergency override requires:

- documented incident id,
- break-glass authorization,
- accepted after-action evidence,
- follow-up compensating change when metadata was temporarily unsafe.
