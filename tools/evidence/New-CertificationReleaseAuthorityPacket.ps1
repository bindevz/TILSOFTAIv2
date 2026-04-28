param(
    [Parameter(Mandatory = $true)]
    [string]$SessionPath,

    [string]$ReviewSummaryPath = "",
    [string]$AcceptancePath = "",
    [string]$OutputRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Missing required artifact: $Path"
    }

    return Get-Content $Path -Raw | ConvertFrom-Json
}

if (-not (Test-Path $SessionPath)) {
    throw "Missing certification execution session: $SessionPath"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Split-Path -Parent $SessionPath
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$session = Read-JsonFile -Path $SessionPath
$review = $null
$acceptance = $null
if (-not [string]::IsNullOrWhiteSpace($ReviewSummaryPath) -and (Test-Path $ReviewSummaryPath)) {
    $review = Read-JsonFile -Path $ReviewSummaryPath
}

if (-not [string]::IsNullOrWhiteSpace($AcceptancePath) -and (Test-Path $AcceptancePath)) {
    $acceptance = Read-JsonFile -Path $AcceptancePath
}

$requiredDrills = @($session.drillLedger | Where-Object { $_.required })
$requiredCount = @($requiredDrills).Count
$trustedAccepted = @($requiredDrills | Where-Object { $_.status -eq "completed" -and [string]$_.trustDecision -eq "accepted" })
$trustedBlocked = @($requiredDrills | Where-Object { $_.status -eq "completed" -and [string]$_.trustDecision -ne "accepted" })
$missingRequired = @($requiredDrills | Where-Object { $_.status -notin @("completed", "waived") })
$coveragePercent = if ($requiredCount -eq 0) { 0 } else { [math]::Round(($trustedAccepted.Count / $requiredCount) * 100, 2) }
$unresolvedWaivers = @($session.waivers | Where-Object { [string]$_.status -ne "active" })
$activeWaivers = @($session.waivers | Where-Object { [string]$_.status -eq "active" })
$nonWaivableBlockers = @($session.blockers | Where-Object { $_ -in @("nonwaivable_waiver_violation", "operator_signoff_incomplete", "fallback_authorization_gap", "trusted_evidence_policy_violation", "trusted_evidence_unverified", "trusted_evidence_unbound", "provider_provenance_gap") })
$trustedReady = [bool]$session.decisionInputs.trustedEvidenceReady
$reviewReady = $null -ne $review -and [string]$review.reviewDecision -eq "ready_for_release_review"
$acceptanceState = if ($null -eq $acceptance) { "not_recorded" } else { [string]$acceptance.acceptanceStatus }

$recommendation = if ($nonWaivableBlockers.Count -gt 0 -or -not $trustedReady -or $session.executionState -ne "completed") {
    "block_promotion"
}
elseif (-not $reviewReady) {
    "await_review_readiness"
}
elseif ($acceptanceState -eq "accepted") {
    "promotion_authority_ready"
}
elseif ($acceptanceState -eq "waived") {
    "promotion_authority_waived"
}
else {
    "ready_for_release_authority_review"
}

$packet = [ordered]@{
    schemaVersion = 1
    packetType = "certification-release-authority-packet"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    releaseId = $session.releaseId
    certificationRunId = $session.certificationRunId
    sessionId = $session.sessionId
    environment = $session.environment
    executionState = $session.executionState
    recommendation = $recommendation
    trustedEvidenceCoverage = [ordered]@{
        requiredDrills = $requiredCount
        trustedAcceptedDrills = $trustedAccepted.Count
        trustedBlockedDrills = $trustedBlocked.Count
        missingRequiredDrills = $missingRequired.Count
        coveragePercent = $coveragePercent
    }
    trustedEvidenceReady = $trustedReady
    trustPolicy = $session.trustPolicy
    unresolvedWaivers = @($unresolvedWaivers)
    activeWaivers = @($activeWaivers)
    nonWaivableBlockers = @($nonWaivableBlockers)
    blockers = @($session.blockers)
    reviewDecision = if ($null -eq $review) { "not_supplied" } else { [string]$review.reviewDecision }
    acceptanceStatus = $acceptanceState
    evidenceDrills = @($session.drillLedger | ForEach-Object {
        [ordered]@{
            drillKind = $_.drillKind
            status = $_.status
            evidenceRecordId = $_.evidenceRecordId
            verificationStatus = $_.verificationStatus
            trustTier = $_.trustTier
            trustDecision = $_.trustDecision
            waiverRef = $_.waiverRef
        }
    })
}

$jsonPath = Join-Path $OutputRoot "certification-release-authority-packet.json"
$mdPath = Join-Path $OutputRoot "certification-release-authority-packet.md"
$packet | ConvertTo-Json -Depth 12 | Set-Content -Path $jsonPath -Encoding utf8

$drillLines = @()
foreach ($drill in $packet.evidenceDrills) {
    $drillLines += "| $($drill.drillKind) | $($drill.status) | $($drill.trustDecision) | $($drill.trustTier) | $($drill.verificationStatus) | $($drill.evidenceRecordId) |"
}

$lines = @(
    "# Certification Release Authority Packet",
    "",
    "| Field | Value |",
    "|-------|-------|",
    "| Release | $($packet.releaseId) |",
    "| Certification run | $($packet.certificationRunId) |",
    "| Session | $($packet.sessionId) |",
    "| Environment | $($packet.environment) |",
    "| Execution state | $($packet.executionState) |",
    "| Trusted evidence ready | $($packet.trustedEvidenceReady) |",
    "| Coverage | $($packet.trustedEvidenceCoverage.trustedAcceptedDrills) / $($packet.trustedEvidenceCoverage.requiredDrills) ($($packet.trustedEvidenceCoverage.coveragePercent)%) |",
    "| Non-waivable blockers | $(if (@($packet.nonWaivableBlockers).Count -eq 0) { 'none' } else { @($packet.nonWaivableBlockers) -join ', ' }) |",
    "| Review decision | $($packet.reviewDecision) |",
    "| Acceptance status | $($packet.acceptanceStatus) |",
    "| Recommendation | $($packet.recommendation) |",
    "",
    "## Drill Trust Status",
    "",
    "| Drill | Status | Trust Decision | Trust Tier | Verification | Evidence Record |",
    "|------|--------|----------------|------------|--------------|-----------------|"
)
$lines += $drillLines
$lines | Set-Content -Path $mdPath -Encoding utf8

[ordered]@{
    packetPath = $jsonPath
    markdownPath = $mdPath
    recommendation = $recommendation
    nonWaivableBlockers = $packet.nonWaivableBlockers
} | ConvertTo-Json -Depth 6
