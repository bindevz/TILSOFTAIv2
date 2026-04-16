# Staging And Prod-Like Certification Execution

This guide is the Sprint 25 operator path for preparing a certification run. It does not replace real staging/prod-like execution; it makes the evidence preparation and review flow repeatable.

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
  -EvidenceRefsPath "release-evidence/release-2026-04-16/evidence-refs.json"
```

For production-like environments, `CatalogSourceMode` should normally be `platform`.

If `mixed` or `bootstrap_only` is observed, include both:

```powershell
-FallbackAuthorized `
-FallbackAuthorizationUri "incident://INC-12345"
```

Without that authorization reference, certification review is blocked.

## 3. Validate The Certification Run

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

## Promotion Blockers

Promotion is blocked when:

- any required drill evidence is missing,
- operator signoff is missing,
- evidence refs are examples or malformed,
- evidence is stale for its freshness window,
- production-like fallback was used without authorization,
- release ids differ across certification manifest and bundle artifacts.
