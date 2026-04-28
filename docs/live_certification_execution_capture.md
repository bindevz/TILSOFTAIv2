# Live Certification Execution Capture

This guide is the Sprint 30 operator path for capturing what actually ran during a live staging/prod-like certification session. It turns drill execution from narrative notes into a machine-reviewable session ledger with trusted evidence provenance per drill.

## 1. Prepare The Certification Run Manifest

Generate and validate the certification run manifest first:

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

./tools/evidence/Test-CertificationRunManifest.ps1 `
  -ManifestPath "release-evidence/release-2026-04-16/certification-run-manifest.json"
```

## 2. Generate The Execution Session Ledger

Create a first-class execution session from the manifest:

```powershell
./tools/evidence/New-CertificationExecutionSession.ps1 `
  -CertificationRunPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -OutputPath "release-evidence/release-2026-04-16/certification-execution-session.json" `
  -TrustedEvidencePath "release-evidence/release-2026-04-16/trusted-evidence-refs.json" `
  -TrustPolicyPath "docs/certification_execution_trust_policy.template.json" `
  -SessionId "release-2026-04-16-staging-execution"
```

The session artifact records drill-by-drill status, evidence refs, trusted evidence identity, artifact hash, verification status, trust tier, provider provenance proof, timestamps, governed waivers, fallback posture, execution context, and completion blockers.

## 3. Validate Execution Completeness

```powershell
./tools/evidence/Test-CertificationExecutionSession.ps1 `
  -SessionPath "release-evidence/release-2026-04-16/certification-execution-session.json"
```

Validation blocks incomplete execution when required drills are missing, stale, dry-run/example, or missing signoff.
Validation also blocks sessions when trusted evidence provenance is incomplete or fails trust policy.

## 4. Generate The Execution Summary

```powershell
./tools/evidence/New-CertificationExecutionSummary.ps1 `
  -SessionPath "release-evidence/release-2026-04-16/certification-execution-session.json" `
  -OutputRoot "release-evidence/release-2026-04-16"
```

Reviewers can start from `certification-execution-summary.md` and `certification-execution-summary.json` to inspect the full drill ledger and blocker state.

## 5. Generate Release Authority Packet

```powershell
./tools/evidence/New-CertificationReleaseAuthorityPacket.ps1 `
  -SessionPath "release-evidence/release-2026-04-16/certification-execution-session.json" `
  -ReviewSummaryPath "release-evidence/release-2026-04-16/certification-review-summary.json" `
  -AcceptancePath "release-evidence/release-2026-04-16/certification-acceptance.json" `
  -OutputRoot "release-evidence/release-2026-04-16"
```

Release authority can use `certification-release-authority-packet.json` and `certification-release-authority-packet.md` as a concise trust-and-governance decision artifact.

## 6. Hand Off To Review And Acceptance

After execution validation passes:

1. Generate/validate the release evidence bundle.
2. Generate/validate the certification review summary.
3. Record live acceptance with the execution session attached:

```powershell
./tools/evidence/New-CertificationAcceptance.ps1 `
  -CertificationRunPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -SummaryPath "release-evidence/release-2026-04-16/certification-review-summary.json" `
  -ExecutionSessionPath "release-evidence/release-2026-04-16/certification-execution-session.json" `
  -BundlePath "release-evidence/release-2026-04-16" `
  -AcceptedBy "operator@example.com" `
  -ApprovedBy "release-approver@example.com"
```

## Promotion Blockers

Promotion remains blocked when:

- required drills are missing or not completed,
- required drill evidence is stale,
- example/dry-run evidence appears in required drills,
- production-like fallback is used without authorization,
- operator signoff drill is incomplete,
- accepted certification is not grounded in a completed execution session.
