param(
    [Parameter(Mandatory = $true)]
    [string]$AcceptancePath,

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

if (-not (Test-Path $AcceptancePath)) {
    throw "Missing certification acceptance artifact: $AcceptancePath"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Split-Path -Parent $AcceptancePath
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$acceptance = Read-JsonFile -Path $AcceptancePath
$now = [DateTimeOffset]::UtcNow
$expiresAt = [DateTimeOffset]::Parse([string]$acceptance.expiresAtUtc)
$expired = $expiresAt -lt $now
$goNoGo = if ($expired) {
    "expired"
}
elseif ($acceptance.acceptanceStatus -eq "accepted" -and @($acceptance.blockers).Count -eq 0) {
    "go"
}
elseif ($acceptance.acceptanceStatus -eq "waived") {
    "waived_go"
}
else {
    "no_go"
}

$summary = [ordered]@{
    schemaVersion = 1
    summaryType = "certification-acceptance-summary"
    releaseId = $acceptance.releaseId
    certificationRunId = $acceptance.certificationRunId
    environment = $acceptance.environment
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    acceptanceStatus = $(if ($expired -and $acceptance.acceptanceStatus -eq "accepted") { "expired" } else { $acceptance.acceptanceStatus })
    goNoGo = $goNoGo
    acceptedBy = $acceptance.acceptedBy
    approvedBy = $acceptance.approvedBy
    acceptedAtUtc = $acceptance.acceptedAtUtc
    expiresAtUtc = $acceptance.expiresAtUtc
    freshnessWindowDays = $acceptance.freshnessWindowDays
    fallbackDecision = $acceptance.fallbackPosture.fallbackDecision
    catalogSourceMode = $acceptance.fallbackPosture.catalogSourceMode
    productionLike = $acceptance.fallbackPosture.productionLike
    fallbackUsed = $acceptance.fallbackPosture.fallbackUsed
    fallbackAuthorized = $acceptance.fallbackPosture.fallbackAuthorized
    executionSessionId = $acceptance.certificationExecutionSession.sessionId
    executionSessionState = $acceptance.certificationExecutionSession.executionState
    executionSessionGoNoGo = $acceptance.certificationExecutionSession.goNoGo
    signoffPresent = $acceptance.decisionInputs.signoffPresent
    requiredEvidenceComplete = $acceptance.decisionInputs.requiredEvidenceComplete
    staleEvidencePresent = $acceptance.decisionInputs.staleEvidencePresent
    exampleEvidencePresent = $acceptance.decisionInputs.exampleEvidencePresent
    executionSessionComplete = $acceptance.decisionInputs.executionSessionComplete
    bundleHash = $acceptance.releaseEvidenceBundle.sha256
    waivers = @($acceptance.waivers)
    blockers = @($acceptance.blockers)
    acceptanceArtifact = "certification-acceptance.json"
}

$jsonPath = Join-Path $OutputRoot "certification-acceptance-summary.json"
$mdPath = Join-Path $OutputRoot "certification-acceptance-summary.md"
$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding utf8

$lines = @(
    "# Certification Acceptance Summary",
    "",
    "| Field | Value |",
    "|-------|-------|",
    "| Release | $($summary.releaseId) |",
    "| Certification run | $($summary.certificationRunId) |",
    "| Environment | $($summary.environment) |",
    "| Acceptance | $($summary.acceptanceStatus) |",
    "| Go / no-go | $($summary.goNoGo) |",
    "| Accepted by | $($summary.acceptedBy) |",
    "| Approved by | $($summary.approvedBy) |",
    "| Expires | $($summary.expiresAtUtc) |",
    "| Fallback decision | $($summary.fallbackDecision) |",
    "| Execution session | $($summary.executionSessionId) / $($summary.executionSessionState) |",
    "| Bundle hash | $($summary.bundleHash) |",
    "| Blockers | $(if (@($summary.blockers).Count -eq 0) { 'none' } else { @($summary.blockers) -join ', ' }) |",
    "",
    "## Waivers",
    "",
    "$(if (@($summary.waivers).Count -eq 0) { 'None.' } else { (@($summary.waivers) | ForEach-Object { '- ' + $_ }) -join [Environment]::NewLine })"
)
$lines | Set-Content -Path $mdPath -Encoding utf8

[ordered]@{
    summaryPath = $jsonPath
    markdownPath = $mdPath
    goNoGo = $goNoGo
    acceptanceStatus = $summary.acceptanceStatus
    blockers = $summary.blockers
} | ConvertTo-Json -Depth 6
