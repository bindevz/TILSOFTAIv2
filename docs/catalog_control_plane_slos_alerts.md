# Catalog Control Plane SLOs And Alerts - Sprint 12

The promotion gate exposes current SLO definitions at `GET /api/platform-catalog/slo-definitions`.

## SLIs And SLOs

| SLI | Default SLO |
|-----|-------------|
| Preview success | At least 99% successful previews per rolling hour. |
| Submit success | At least 99% successful submits per rolling hour. |
| Approval success | At least 99% successful approvals per rolling hour. |
| Apply success | At least 99% successful applies per rolling hour. |
| Rollback readiness | Compensating rollback change can be previewed within 30 minutes. |

## Alert Conditions

| Alert | Default condition | Escalation |
|-------|-------------------|------------|
| Production fallback source mode | `mixed` or `bootstrap_only` in production-like environment | Platform on-call and release manager. |
| Version conflict storm | 3 or more version conflicts per hour | Platform on-call reviews pending changes and expected-version usage. |
| Duplicate submit storm | 5 or more duplicate submits per hour | Platform on-call checks CI idempotency key reuse. |
| Apply failure | 1 or more apply failures per hour | Platform/database on-call checks SQL availability and version drift. |
| Rollback surge | 2 or more rollback-linked changes per hour | Incident commander opens after-action review. |
| Untrusted evidence | Any production-like promotion has missing or untrusted required evidence | Release authority blocks manifest issuance. |
| Rollout attestation gap | Production-like completion lacks trusted attestation evidence | Release manager blocks completion sign-off. |

## Metrics

| Metric | Purpose |
|--------|---------|
| `tilsoftai_platform_catalog_promotion_gate_total` | Counts allowed/blocked promotion gate evaluations by environment and source mode. |
| `tilsoftai_platform_catalog_certification_evidence_total` | Counts certification evidence records by environment, kind, and status. |
| `tilsoftai_platform_catalog_mutations_total` | Counts preview/submit/review/apply operations by risk, environment, and outcome. |
| `tilsoftai_platform_catalog_source_mode_total` | Reports startup source mode and production-like posture. |

## Operator Response

Every alert must lead to one of:

- rejected promotion,
- corrected preview payload,
- retried idempotent submit/apply,
- rollback-by-compensating-change,
- incident and after-action evidence.

Alerts without a named response are not accepted as Sprint 12 control-plane observability.
