param(
    [Parameter(Mandatory = $true)]
    [string]$SessionPath,

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
$now = [DateTimeOffset]::UtcNow
$missingDrills = @($session.drillLedger | Where-Object { $_.required -and $_.status -notin @("completed", "waived") } | ForEach-Object { [string]$_.drillKind })
$exampleDrills = @($session.drillLedger | Where-Object { $_.status -in @("example", "blocked_example") -or ([string]$_.evidenceUri) -match "example" } | ForEach-Object { [string]$_.drillKind })
$staleDrills = @()
foreach ($entry in $session.drillLedger) {
    if ([string]::IsNullOrWhiteSpace([string]$entry.collectedAtUtc)) {
        continue
    }

    $collected = [DateTimeOffset]::Parse([string]$entry.collectedAtUtc)
    if ($collected.AddDays([int]$entry.freshnessWindowDays) -lt $now) {
        $staleDrills += [string]$entry.drillKind
    }
}

$waivedDrills = @($session.drillLedger | Where-Object { $_.status -eq "waived" } | ForEach-Object { [string]$_.drillKind })
$completedDrills = @($session.drillLedger | Where-Object { $_.status -eq "completed" } | ForEach-Object { [string]$_.drillKind })
$trustedAcceptedDrills = @($session.drillLedger | Where-Object { $_.status -eq "completed" -and [string]$_.trustDecision -eq "accepted" } | ForEach-Object { [string]$_.drillKind })
$trustedBlockedDrills = @($session.drillLedger | Where-Object { $_.status -eq "completed" -and [string]$_.trustDecision -ne "accepted" } | ForEach-Object { [string]$_.drillKind })
$goNoGo = if ($session.executionState -eq "completed" -and $missingDrills.Count -eq 0 -and $exampleDrills.Count -eq 0 -and $staleDrills.Count -eq 0 -and [bool]$session.decisionInputs.trustedEvidenceReady) {
    "ready_for_acceptance"
}
else {
    "blocked"
}

$summary = [ordered]@{
    schemaVersion = 1
    summaryType = "certification-execution-summary"
    sessionId = $session.sessionId
    releaseId = $session.releaseId
    certificationRunId = $session.certificationRunId
    environment = $session.environment
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    executionState = $session.executionState
    goNoGo = $goNoGo
    sessionStartedAtUtc = $session.sessionStartedAtUtc
    sessionCompletedAtUtc = $session.sessionCompletedAtUtc
    executionWindowId = $session.executionContext.executionWindowId
    operatorId = $session.executionContext.operatorId
    approverId = $session.executionContext.approverId
    fallbackDecision = $session.fallbackPosture.fallbackDecision
    drillCount = @($session.drillLedger).Count
    completedDrills = $completedDrills
    waivedDrills = $waivedDrills
    missingRequiredDrills = $missingDrills
    staleDrills = $staleDrills
    exampleDrills = $exampleDrills
    trustedAcceptedDrills = $trustedAcceptedDrills
    trustedBlockedDrills = $trustedBlockedDrills
    trustedEvidenceReady = [bool]$session.decisionInputs.trustedEvidenceReady
    trustPolicy = $session.trustPolicy
    blockers = @($session.blockers)
    waivers = @($session.waivers)
    executionSessionArtifact = "certification-execution-session.json"
}

$jsonPath = Join-Path $OutputRoot "certification-execution-summary.json"
$mdPath = Join-Path $OutputRoot "certification-execution-summary.md"
$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding utf8

$drillLines = @()
foreach ($entry in $session.drillLedger) {
    $drillLines += "| $($entry.drillKind) | $($entry.status) | $($entry.trustDecision) | $($entry.evidenceRecordId) | $($entry.verifierClass)/$($entry.trustTier) | $($entry.evidenceUri) | $($entry.collectedAtUtc) | $($entry.freshnessWindowDays) |"
}

$lines = @(
    "# Certification Execution Summary",
    "",
    "| Field | Value |",
    "|-------|-------|",
    "| Session | $($summary.sessionId) |",
    "| Release | $($summary.releaseId) |",
    "| Certification run | $($summary.certificationRunId) |",
    "| Environment | $($summary.environment) |",
    "| Execution state | $($summary.executionState) |",
    "| Decision | $($summary.goNoGo) |",
    "| Fallback decision | $($summary.fallbackDecision) |",
    "| Trusted evidence ready | $($summary.trustedEvidenceReady) |",
    "| Trusted accepted drills | $(@($summary.trustedAcceptedDrills).Count) |",
    "| Trusted blocked drills | $(@($summary.trustedBlockedDrills).Count) |",
    "| Blockers | $(if (@($summary.blockers).Count -eq 0) { 'none' } else { @($summary.blockers) -join ', ' }) |",
    "",
    "## Drill Ledger",
    "",
    "| Drill | Status | Trust Decision | Evidence Record | Verifier/Tier | Evidence URI | Collected At | Freshness (days) |",
    "|------|--------|----------------|-----------------|---------------|--------------|--------------|------------------|"
)

$lines += $drillLines
$lines += @(
    "",
    "## Missing Required Drills",
    "",
    "$(if (@($summary.missingRequiredDrills).Count -eq 0) { 'None.' } else { @($summary.missingRequiredDrills | ForEach-Object { '- ' + $_ }) -join [Environment]::NewLine })"
)

$lines | Set-Content -Path $mdPath -Encoding utf8

[ordered]@{
    summaryPath = $jsonPath
    markdownPath = $mdPath
    goNoGo = $goNoGo
    missingRequiredDrills = $summary.missingRequiredDrills
    blockers = $summary.blockers
} | ConvertTo-Json -Depth 6
