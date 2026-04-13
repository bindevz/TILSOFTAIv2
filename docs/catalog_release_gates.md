# Catalog Release Gates - Sprint 12

Catalog changes must pass promotion gates and issue an immutable promotion manifest before production-like rollout.

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
- required certification evidence is present but untrusted.
- required certification evidence has insufficient trust tier.
- required certification evidence is stale.
- production-like rollout completion lacks an archived dossier.

## CI/CD Usage

1. Run preview gate with the proposed payload.
2. Submit only if `isAllowed=true`.
3. After approval, run change gate with `changeId`.
4. Verify and accept required evidence, using active lifecycle-managed signer keys for high-assurance production-like releases.
5. Issue a promotion manifest with the approved change ids and trusted evidence ids.
6. Archive the promotion dossier.
7. Replay-verify the archived dossier package.
8. Confirm signer trust-store backup after any signer lifecycle change.
9. Apply only with manifest-backed release approval.
10. Record rollout attestations and completion evidence.

The gate returns deterministic `blockers`, `warnings`, `evidenceMissing`, and `evidenceUntrusted` arrays for automation and operator review.

## Human Override

Do not bypass the gate for convenience. Emergency override requires:

- documented incident id,
- break-glass authorization,
- accepted after-action evidence,
- follow-up compensating change when metadata was temporarily unsafe.
