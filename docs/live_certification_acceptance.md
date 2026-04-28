# Live Certification Acceptance

This guide is the Sprint 30 operator path for recording accepted staging/prod-like certification. It starts only after the certification run manifest, execution session ledger, release evidence bundle, certification review summary, and trusted evidence policy checks have been generated and validated with real, non-example evidence.

Capture execution first by following `docs/live_certification_execution_capture.md`.

## 1. Confirm Review Readiness

Validate the certification review summary without dry-run allowances:

```powershell
./tools/evidence/Test-CertificationReviewSummary.ps1 `
  -SummaryPath "release-evidence/release-2026-04-16/certification-review-summary.json"
```

The summary must be `ready_for_release_review`. Do not record live acceptance for example evidence, dry-run manifests, missing drill evidence, stale evidence, missing signoff, or unacceptable fallback posture.

## 2. Generate The Acceptance Artifact

```powershell
./tools/evidence/New-CertificationAcceptance.ps1 `
  -CertificationRunPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -SummaryPath "release-evidence/release-2026-04-16/certification-review-summary.json" `
  -ExecutionSessionPath "release-evidence/release-2026-04-16/certification-execution-session.json" `
  -BundlePath "release-evidence/release-2026-04-16" `
  -AcceptedBy "operator@example.com" `
  -ApprovedBy "release-approver@example.com" `
  -FreshnessWindowDays 14
```

The generated `certification-acceptance.json` records release scope, environment, accepted-by and approved-by identities, certification run id, execution session id/state, bundle hash, artifact hashes, fallback posture, signoff state, trust decision inputs, expiry, governed waivers, and blockers.

## 3. Validate Live Acceptance

```powershell
./tools/evidence/Test-CertificationAcceptance.ps1 `
  -AcceptancePath "release-evidence/release-2026-04-16/certification-acceptance.json"
```

Validation fails when the artifact is blocked, expired, missing signoff, missing accepted/approved identities, using example or dry-run evidence, using stale evidence, referencing bundle artifacts whose hashes no longer match, not grounded in a completed execution session, or carrying incomplete trusted evidence semantics.

## 4. Generate The Go/No-Go Summary

```powershell
./tools/evidence/New-CertificationAcceptanceSummary.ps1 `
  -AcceptancePath "release-evidence/release-2026-04-16/certification-acceptance.json" `
  -OutputRoot "release-evidence/release-2026-04-16"
```

Release governance can start from `certification-acceptance-summary.md` and `certification-acceptance-summary.json`. The summary states the go/no-go decision, expiry, fallback posture, signoff state, bundle hash, waivers, and blockers.

## 5. Validate Release Evidence With Accepted Certification

```powershell
./tools/evidence/Test-ReleaseEvidenceBundle.ps1 `
  -BundlePath "release-evidence/release-2026-04-16" `
  -RequireAcceptedCertification
```

This companion gate detects production-like release evidence packages that do not contain the expected accepted certification artifact.

## Expiry And Renewal

When `expiresAtUtc` passes before release review completes, regenerate the certification run from fresh evidence or repeat the required drills. Do not extend expiry by editing the acceptance artifact; create a new acceptance artifact from freshly validated inputs.

## Waivers

Waivers must be governed objects (id, authority, scope, reason, expiry, linked drills). Dry-run/example evidence, missing signoff, missing acceptor/approver identity, unacceptable production-like fallback, invalid waiver objects, and trusted-evidence policy violations remain non-waivable live-certification blockers.
