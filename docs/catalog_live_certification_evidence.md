# Catalog Live Certification Evidence

The platform has a durable evidence path for staging and prod-like certification. This document is the certification package index; it must be filled with real evidence from the target environment before production promotion is accepted.

Sprint 24 adds executable release evidence bundle generation. Use `tools/evidence/New-ReleaseEvidenceBundle.ps1` to assemble the release evidence bundle and `tools/evidence/Test-ReleaseEvidenceBundle.ps1` to validate it before release review.

## Required Evidence Kinds

| Evidence kind | Required proof |
|---------------|----------------|
| `runbook_execution` | Control-plane runbook executed end to end against the target SQL catalog. |
| `preview_failure_drill` | Invalid payload preview blocked before submit. |
| `version_conflict_drill` | Stale `ExpectedVersionTag` blocked before promotion/apply. |
| `duplicate_submit_drill` | Replayed submit returned the existing pending change. |
| `sql_apply_outage_drill` | Approved change stayed recoverable while SQL was unavailable during apply. |
| `fallback_risk_drill` | Production-like mixed/bootstrap-only source mode blocked promotion. |
| `operator_signoff` | Named operator and approver accepted the evidence package. |

## Evidence API

Use `POST /api/platform-catalog/certification-evidence` to record evidence:

```json
{
  "environmentName": "staging",
  "evidenceKind": "runbook_execution",
  "status": "accepted",
  "summary": "Runbook completed against staging SQL catalog; change CHG-123 previewed, submitted, approved, applied, and verified.",
  "evidenceUri": "https://evidence.example/CHG-123",
  "relatedChangeId": "catalog-change-id",
  "relatedIncidentId": "",
  "approvedByUserId": "ops-lead"
}
```

Evidence statuses:

- `accepted`: may satisfy a promotion gate.
- `pending`: captured but not yet accepted.
- `rejected`: captured and explicitly not accepted.

## Promotion Acceptance

Production-like promotion is blocked until every configured evidence kind has at least one trusted record for that environment.

Sprint 13 trusted evidence requires:

- lifecycle status accepted by a verifier or release authority,
- `VerificationStatus=verified`,
- trust tier meets the target environment requirement,
- allowed evidence URI prefix,
- SHA-256 artifact hash,
- provider-recomputed artifact hash when the URI is provider-backed,
- source system and content metadata when supplied,
- non-stale collection timestamp.

Do not mark evidence as accepted unless it came from a real staging or prod-like execution. Synthetic unit test output is not live certification evidence.

## Bundle Attachment

For release review, attach the generated bundle described in `docs/release_evidence_bundles.md`. The bundle captures required certification evidence references, compatibility inventory hash, compatibility readiness output references, fallback source-mode posture, rollback posture, and validation output references.
