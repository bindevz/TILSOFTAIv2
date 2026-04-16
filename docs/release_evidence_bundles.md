# Release Evidence Bundles

Release evidence bundles are the Sprint 24 convention for collecting operational proof in a repeatable, reviewable shape.

## Bundle Shape

Generated bundles live under:

```text
release-evidence/<release-id>/
```

Each bundle contains:

| File | Purpose |
|------|---------|
| `db-major-readiness-evidence-packet.json` | Generated readiness packet with release id, environment, compatibility inventory hash, telemetry references, rollback posture, and validation posture. |
| `certification-evidence-manifest.json` | Required certification evidence kinds and the artifact/ticket URI for each kind. |
| `fallback-posture.json` | Platform catalog source mode, production-like flag, fallback usage, fallback authorization URI, and pass/fail posture. |
| `validation-results.json` | Evidence-generation metadata and validation result references. |

The generated bundle directory is intentionally ignored by git. Attach the completed bundle to the release record or publish it to the approved evidence store.

## Generate A Bundle

For release review, prefer generating a certification run manifest first:

```powershell
./tools/evidence/New-CertificationRunManifest.ps1 `
  -ReleaseId "release-2026-04-16" `
  -Environment "staging" `
  -OutputPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -CatalogSourceMode "platform" `
  -EvidenceRefsPath "release-evidence/release-2026-04-16/evidence-refs.json"
```

Then generate the bundle from that manifest:

```powershell
./tools/evidence/New-ReleaseEvidenceBundle.ps1 `
  -ReleaseId "release-2026-04-16" `
  -Environment "staging" `
  -CertificationRunPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -WindowStartUtc "2026-04-01T00:00:00Z" `
  -WindowEndUtc "2026-04-16T00:00:00Z" `
  -UsageSummaryUri "artifact://release-evidence/release-2026-04-16/usage-summary.json" `
  -RetirementReadinessUri "artifact://release-evidence/release-2026-04-16/readiness.json" `
  -RollbackPlanUri "artifact://release-evidence/release-2026-04-16/rollback.md" `
  -OperatorCommunicationUri "artifact://release-evidence/release-2026-04-16/operator-communication.md"
```

## Validate A Bundle

```powershell
./tools/evidence/Test-ReleaseEvidenceBundle.ps1 `
  -BundlePath "release-evidence/release-2026-04-16"
```

Validation fails when:

- required files are missing,
- release id or environment is empty,
- compatibility inventory hash is missing,
- required certification evidence is missing,
- production-like fallback was used without authorization evidence.

Use `-AllowMissingEvidence` only for local dry runs. Release review should validate without that switch.

## Generate Review Summary

```powershell
./tools/evidence/New-CertificationReviewSummary.ps1 `
  -CertificationRunPath "release-evidence/release-2026-04-16/certification-run-manifest.json" `
  -BundlePath "release-evidence/release-2026-04-16"
```

The summary emits machine-readable `certification-review-summary.json` and operator-readable `certification-review-summary.md`.

## Fallback Posture

`CatalogSourceMode=platform` is the normal production-like posture.

`mixed` or `bootstrap_only` means fallback was involved. In production-like environments this must either:

- fail validation, or
- include explicit fallback authorization evidence and incident/release references.

## Signed Artifact Verification Decision

Sprint 24 keeps full publisher-signature verification deferred. Evidence bundles capture inventory hashes and artifact references now; `signature_verified` remains reserved for a compliance-driven implementation when publisher signature proof is required.
