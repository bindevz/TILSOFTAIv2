# Certification Release Authority Packet

Sprint 30 adds a concise release-authority packet so release governance can make a trust-aware go/no-go decision without reading multiple artifacts manually.

## Generate Packet

```powershell
./tools/evidence/New-CertificationReleaseAuthorityPacket.ps1 `
  -SessionPath "release-evidence/release-2026-04-16/certification-execution-session.json" `
  -ReviewSummaryPath "release-evidence/release-2026-04-16/certification-review-summary.json" `
  -AcceptancePath "release-evidence/release-2026-04-16/certification-acceptance.json" `
  -OutputRoot "release-evidence/release-2026-04-16"
```

## Output Artifacts

- `certification-release-authority-packet.json`
- `certification-release-authority-packet.md`

## Packet Contents

- trusted evidence coverage (required vs trusted accepted),
- drill-by-drill trust decision summary,
- unresolved and active waiver state,
- non-waivable blockers,
- review and acceptance state,
- final release-authority recommendation.

## Recommendation Semantics

- `block_promotion`: non-waivable blockers, trusted evidence gaps, or incomplete execution state.
- `await_review_readiness`: trust is complete but review summary is not release-ready yet.
- `ready_for_release_authority_review`: execution and trust are ready; acceptance is not recorded yet.
- `promotion_authority_ready`: acceptance is recorded as accepted.
- `promotion_authority_waived`: acceptance is recorded as waived.
