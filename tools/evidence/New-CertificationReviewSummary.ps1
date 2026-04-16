param(
    [Parameter(Mandatory = $true)]
    [string]$CertificationRunPath,

    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

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

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = $BundlePath
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$certification = Read-JsonFile -Path $CertificationRunPath
$packet = Read-JsonFile -Path (Join-Path $BundlePath "db-major-readiness-evidence-packet.json")
$bundleCertification = Read-JsonFile -Path (Join-Path $BundlePath "certification-evidence-manifest.json")
$fallback = Read-JsonFile -Path (Join-Path $BundlePath "fallback-posture.json")
$validation = Read-JsonFile -Path (Join-Path $BundlePath "validation-results.json")

$missingEvidence = @($certification.requiredEvidence | Where-Object { $_.status -eq "missing" -or [string]::IsNullOrWhiteSpace([string]$_.evidenceUri) })
$exampleEvidence = @($certification.requiredEvidence | Where-Object { $_.status -eq "example" -or $_.status -eq "blocked_example" -or ([string]$_.evidenceUri) -match "example" })
$fallbackBlocked = $certification.fallbackPosture.productionLike -and $certification.fallbackPosture.fallbackUsed -and $certification.fallbackPosture.fallbackDecision -ne "authorized_exception"
$signoffMissing = $certification.signoff.required -and [string]::IsNullOrWhiteSpace([string]$certification.signoff.signoffUri)
$staleEvidence = @()
$now = [DateTimeOffset]::UtcNow

foreach ($item in $certification.requiredEvidence) {
    if ([string]::IsNullOrWhiteSpace([string]$item.collectedAtUtc)) {
        continue
    }

    $collectedAt = [DateTimeOffset]::Parse([string]$item.collectedAtUtc)
    $freshnessDays = [int]$item.freshnessWindowDays
    if ($collectedAt.AddDays($freshnessDays) -lt $now) {
        $staleEvidence += $item.evidenceKind
    }
}

$blockers = @()
if ($missingEvidence.Count -gt 0) {
    $blockers += "missing_evidence"
}
if ($exampleEvidence.Count -gt 0) {
    $blockers += "example_evidence"
}
if ($fallbackBlocked) {
    $blockers += "fallback_authorization_gap"
}
if ($signoffMissing) {
    $blockers += "operator_signoff_missing"
}
if ($staleEvidence.Count -gt 0) {
    $blockers += "stale_evidence"
}
if ($packet.releaseId -ne $certification.releaseId -or $fallback.releaseId -ne $certification.releaseId -or $validation.releaseId -ne $certification.releaseId) {
    $blockers += "release_id_mismatch"
}
if ($bundleCertification.requiredEvidence.Count -ne $certification.requiredEvidence.Count) {
    $blockers += "bundle_certification_manifest_mismatch"
}

$reviewDecision = if ($blockers.Count -eq 0 -and $certification.reviewState -eq "ready_for_review") {
    "ready_for_release_review"
}
else {
    "blocked"
}

$summary = [ordered]@{
    schemaVersion = 1
    summaryType = "certification-review-summary"
    releaseId = $certification.releaseId
    certificationRunId = $certification.certificationRunId
    environment = $certification.environment
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    reviewDecision = $reviewDecision
    reviewState = $certification.reviewState
    fallbackDecision = $certification.fallbackPosture.fallbackDecision
    fallbackUsed = $certification.fallbackPosture.fallbackUsed
    fallbackAuthorized = $certification.fallbackPosture.fallbackAuthorized
    missingEvidenceKinds = @($missingEvidence | ForEach-Object { $_.evidenceKind })
    exampleEvidenceKinds = @($exampleEvidence | ForEach-Object { $_.evidenceKind })
    staleEvidenceKinds = $staleEvidence
    blockers = $blockers
    readinessPacket = "db-major-readiness-evidence-packet.json"
    certificationManifest = "certification-evidence-manifest.json"
    fallbackPosture = "fallback-posture.json"
}

$jsonPath = Join-Path $OutputRoot "certification-review-summary.json"
$mdPath = Join-Path $OutputRoot "certification-review-summary.md"
$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding utf8

$lines = @(
    "# Certification Review Summary",
    "",
    "| Field | Value |",
    "|-------|-------|",
    "| Release | $($summary.releaseId) |",
    "| Certification run | $($summary.certificationRunId) |",
    "| Environment | $($summary.environment) |",
    "| Decision | $($summary.reviewDecision) |",
    "| Review state | $($summary.reviewState) |",
    "| Fallback decision | $($summary.fallbackDecision) |",
    "| Blockers | $(if ($blockers.Count -eq 0) { 'none' } else { $blockers -join ', ' }) |",
    "",
    "## Missing Evidence",
    "",
    "$(if ($missingEvidence.Count -eq 0) { 'None.' } else { ($missingEvidence | ForEach-Object { '- ' + $_.evidenceKind }) -join [Environment]::NewLine })",
    "",
    "## Stale Evidence",
    "",
    "$(if ($staleEvidence.Count -eq 0) { 'None.' } else { ($staleEvidence | ForEach-Object { '- ' + $_ }) -join [Environment]::NewLine })"
)
$lines | Set-Content -Path $mdPath -Encoding utf8

[ordered]@{
    summaryPath = $jsonPath
    markdownPath = $mdPath
    reviewDecision = $reviewDecision
    blockers = $blockers
} | ConvertTo-Json -Depth 6
