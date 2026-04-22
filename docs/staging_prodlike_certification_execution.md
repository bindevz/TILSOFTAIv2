# Staging And Prod-Like Certification Execution

This guide is the Sprint 29 operator path for preparing a certification run, capturing drill execution, and handing it to live-certification acceptance. It does not replace real staging/prod-like execution; it makes the evidence preparation, execution capture, review gate, and acceptance handoff repeatable.

## 1. Gather Evidence References

Run the catalog runbook and required failure drills in the target environment. Capture one durable URI or ticket id for each required evidence kind:

- `runbook_execution`
- `preview_failure_drill`
- `version_conflict_drill`
- `duplicate_submit_drill`
- `sql_apply_outage_drill`
- `fallback_risk_drill`
- `operator_signoff`

Use `docs/certification_evidence_refs.example.json` as the shape, but do not use example refs for release review.

## 2. Generate The Certification Run Manifest

```powershell
./tools/evidence/New-CertificationRunManifest.ps1 `
  -ReleaseId "release-2026-04-16" `
  -Environment "staging" `
  -OutputPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -CatalogSourceMode "platform" `
  -ChangeTicketId "CHANGE-12345" `
  -TenantScopeRef "artifact://release-evidence/release-2026-04-16/tenant-scope.json" `
  -ExecutionWindowId "staging-window-2026-04-16" `
  -OperatorId "operator@example.com" `
  -ApproverId "approver@example.com" `
  -EvidenceRefsPath "release-evidence/release-2026-04-16/evidence-refs.json"
```

For production-like environments, `CatalogSourceMode` should normally be `platform`.

If `mixed` or `bootstrap_only` is observed, include both:

```powershell
-FallbackAuthorized `
-FallbackAuthorizationUri "incident://INC-12345"
```

Without that authorization reference, certification review is blocked.

The generated manifest includes `executionContext` placeholders and a `reviewGate` decision. For live review, `reviewGate.acceptedForReleaseReview` must be `true`; dry-run/example manifests must not be treated as release evidence.

## 3. Validate The Certification Run Manifest

```powershell
./tools/evidence/Test-CertificationRunManifest.ps1 `
  -ManifestPath "release-evidence/release-2026-04-16/certification-run-manifest.json"
```

Validation blocks release prep when evidence is missing, example refs are used, evidence URIs are malformed, fallback authorization is missing, or operator signoff is absent.

## 4. Generate The Release Evidence Bundle

```powershell
./tools/evidence/New-ReleaseEvidenceBundle.ps1 `
  -ReleaseId "release-2026-04-16" `
  -Environment "staging" `
  -CertificationRunPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -UsageSummaryUri "artifact://release-evidence/release-2026-04-16/usage-summary.json" `
  -RetirementReadinessUri "artifact://release-evidence/release-2026-04-16/readiness.json" `
  -RollbackPlanUri "artifact://release-evidence/release-2026-04-16/rollback.md" `
  -OperatorCommunicationUri "artifact://release-evidence/release-2026-04-16/operator-communication.md"
```

## 5. Validate The Bundle

```powershell
./tools/evidence/Test-ReleaseEvidenceBundle.ps1 `
  -BundlePath "release-evidence/release-2026-04-16"
```

## 6. Generate Review Summary

```powershell
./tools/evidence/New-CertificationReviewSummary.ps1 `
  -CertificationRunPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -BundlePath "release-evidence/release-2026-04-16"
```

Release review starts with `certification-review-summary.md` and `certification-review-summary.json`.

Validate the generated review gate before presenting the package for release review:

```powershell
./tools/evidence/Test-CertificationReviewSummary.ps1 `
  -SummaryPath "release-evidence/release-2026-04-16/certification-review-summary.json"
```

Use `-AllowBlocked` only in local/CI smoke checks where the purpose is to prove blocker detection, not to approve a release.

## 7. Capture Execution Session

After the manifest validates, record what actually ran:

```powershell
./tools/evidence/New-CertificationExecutionSession.ps1 `
  -CertificationRunPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -OutputPath "release-evidence/release-2026-04-16/certification-execution-session.json"

./tools/evidence/Test-CertificationExecutionSession.ps1 `
  -SessionPath "release-evidence/release-2026-04-16/certification-execution-session.json"

./tools/evidence/New-CertificationExecutionSummary.ps1 `
  -SessionPath "release-evidence/release-2026-04-16/certification-execution-session.json" `
  -OutputRoot "release-evidence/release-2026-04-16"
```

Execution must be completed drill-by-drill before live acceptance.

## 8. Record Live Acceptance

Only after the review summary validates without allowances, follow `docs/live_certification_acceptance.md`:

```powershell
./tools/evidence/New-CertificationAcceptance.ps1 `
  -CertificationRunPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -SummaryPath "release-evidence/release-2026-04-16/certification-review-summary.json" `
  -ExecutionSessionPath "release-evidence/release-2026-04-16/certification-execution-session.json" `
  -BundlePath "release-evidence/release-2026-04-16" `
  -AcceptedBy "operator@example.com" `
  -ApprovedBy "release-approver@example.com"

./tools/evidence/Test-CertificationAcceptance.ps1 `
  -AcceptancePath "release-evidence/release-2026-04-16/certification-acceptance.json"

./tools/evidence/New-CertificationAcceptanceSummary.ps1 `
  -AcceptancePath "release-evidence/release-2026-04-16/certification-acceptance.json" `
  -OutputRoot "release-evidence/release-2026-04-16"
```

The acceptance artifact is the release-governance handoff from review-ready certification to accepted live certification.

## Promotion Blockers

Promotion is blocked when:

- any required drill evidence is missing,
- operator signoff is missing,
- evidence refs are examples or malformed,
- evidence is stale for its freshness window,
- freshness windows are absent or invalid,
- production-like fallback was used without authorization,
- release ids or certification run ids differ across certification manifest and bundle artifacts,
- the certification manifest review gate is not `ready_for_review`,
- required drill execution session is missing or incomplete,
- live acceptance is missing, blocked, expired, or based on dry-run/example evidence.
