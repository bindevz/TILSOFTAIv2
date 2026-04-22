param(
    [Parameter(Mandatory = $true)]
    [string]$CertificationRunPath,

    [string]$OutputPath = "",
    [string]$SessionId = "",
    [string]$SessionStartedAtUtc = "",
    [string]$SessionCompletedAtUtc = "",
    [string[]]$WaivedDrillKinds = @(),
    [string]$WaiverNote = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$requiredDrills = @(
    "runbook_execution",
    "preview_failure_drill",
    "version_conflict_drill",
    "duplicate_submit_drill",
    "sql_apply_outage_drill",
    "fallback_risk_drill",
    "operator_signoff"
)

function Resolve-RepoRoot {
    $directory = Get-Item (Get-Location).Path
    while ($null -ne $directory) {
        if (Test-Path (Join-Path $directory.FullName "TILSOFTAI.slnx")) {
            return $directory.FullName
        }

        $directory = $directory.Parent
    }

    throw "Could not locate repository root."
}

function Resolve-ArtifactPath {
    param(
        [string]$Path,
        [string]$RepoRoot
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $RepoRoot $Path
}

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Missing required artifact: $Path"
    }

    return Get-Content $Path -Raw | ConvertFrom-Json
}

$repoRoot = Resolve-RepoRoot
$templatePath = Join-Path $repoRoot "docs/certification_execution_session.template.json"
if (-not (Test-Path $templatePath)) {
    throw "Missing certification execution session template."
}

$resolvedCertificationRunPath = Resolve-ArtifactPath -Path $CertificationRunPath -RepoRoot $repoRoot
$certification = Read-JsonFile -Path $resolvedCertificationRunPath

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path -Parent $resolvedCertificationRunPath) "certification-execution-session.json"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repoRoot $OutputPath
}

$now = [DateTimeOffset]::UtcNow
if ([string]::IsNullOrWhiteSpace($SessionStartedAtUtc)) {
    $SessionStartedAtUtc = if ([string]::IsNullOrWhiteSpace([string]$certification.generatedAtUtc)) { $now.ToString("o") } else { [string]$certification.generatedAtUtc }
}

if ([string]::IsNullOrWhiteSpace($SessionId)) {
    $SessionId = "$($certification.releaseId)-$($certification.environment)-execution"
}

$session = Get-Content $templatePath -Raw | ConvertFrom-Json
$waivedLookup = @{}
foreach ($drill in $WaivedDrillKinds) {
    if (-not [string]::IsNullOrWhiteSpace($drill)) {
        $waivedLookup[$drill] = $true
    }
}

$sourceEvidence = @{}
foreach ($item in $certification.requiredEvidence) {
    $sourceEvidence[[string]$item.evidenceKind] = $item
}

$staleDrills = New-Object System.Collections.Generic.List[string]
$missingRequired = New-Object System.Collections.Generic.List[string]
$exampleDrills = New-Object System.Collections.Generic.List[string]
$waiverEntries = @()
foreach ($entry in $session.drillLedger) {
    $drillKind = [string]$entry.drillKind
    $source = if ($sourceEvidence.ContainsKey($drillKind)) { $sourceEvidence[$drillKind] } else { $null }
    $evidenceUri = if ($null -eq $source) { "" } else { [string]$source.evidenceUri }
    $sourceStatus = if ($null -eq $source) { "missing" } else { [string]$source.status }
    $collectedAt = if ($null -eq $source) { "" } else { [string]$source.collectedAtUtc }
    $freshness = if ($null -eq $source) { [int]$entry.freshnessWindowDays } else { [int]$source.freshnessWindowDays }
    if ($freshness -le 0) {
        $freshness = if ($drillKind -eq "operator_signoff") { 14 } else { 30 }
    }

    $entry.evidenceUri = $evidenceUri
    $entry.collectedAtUtc = $collectedAt
    $entry.freshnessWindowDays = $freshness
    $entry.note = ""
    $entry.waiverRef = ""

    if ($waivedLookup.ContainsKey($drillKind)) {
        $entry.status = "waived"
        $entry.note = if ([string]::IsNullOrWhiteSpace($WaiverNote)) { "Waived by release authority." } else { $WaiverNote }
        $entry.waiverRef = "waiver://$drillKind"
        $waiverEntries += [ordered]@{
            drillKind = $drillKind
            waiverRef = $entry.waiverRef
            note = $entry.note
        }
    }
    elseif ([string]::IsNullOrWhiteSpace($evidenceUri)) {
        $entry.status = "missing"
        if ([bool]$entry.required) {
            $missingRequired.Add($drillKind)
        }
    }
    elseif ($sourceStatus -in @("example", "blocked_example") -or $evidenceUri -match "example") {
        $entry.status = "example"
        $exampleDrills.Add($drillKind)
    }
    else {
        $entry.status = "completed"
    }

    if ($entry.status -in @("completed", "waived") -and -not [string]::IsNullOrWhiteSpace($entry.collectedAtUtc)) {
        $collected = [DateTimeOffset]::Parse([string]$entry.collectedAtUtc)
        if ($collected.AddDays([int]$entry.freshnessWindowDays) -lt $now) {
            $staleDrills.Add($drillKind)
        }
    }
}

$requiredPresent = @($session.drillLedger | Where-Object { $_.required }).Count -eq $requiredDrills.Count
$requiredCompleted = @($session.drillLedger | Where-Object { $_.required -and $_.status -notin @("completed", "waived") }).Count -eq 0
$examplePresent = $exampleDrills.Count -gt 0
$stalePresent = $staleDrills.Count -gt 0
$fallback = $certification.fallbackPosture
$fallbackAccepted = -not ($fallback.productionLike -and $fallback.fallbackUsed -and $fallback.fallbackDecision -ne "authorized_exception")
$signoffEntry = $session.drillLedger | Where-Object { $_.drillKind -eq "operator_signoff" } | Select-Object -First 1
$signoffCompleted = $null -ne $signoffEntry -and $signoffEntry.status -in @("completed", "waived")

$blockers = @()
if (-not $requiredPresent) {
    $blockers += "required_drill_set_incomplete"
}
if ($missingRequired.Count -gt 0) {
    $blockers += "missing_required_drill"
}
if ($examplePresent) {
    $blockers += "example_evidence"
}
if ($stalePresent) {
    $blockers += "stale_evidence"
}
if (-not $fallbackAccepted) {
    $blockers += "fallback_authorization_gap"
}
if (-not $signoffCompleted) {
    $blockers += "operator_signoff_incomplete"
}

$session.releaseId = $certification.releaseId
$session.certificationRunId = $certification.certificationRunId
$session.environment = $certification.environment
$session.sessionId = $SessionId
$session.generatedAtUtc = $now.ToString("o")
$session.sessionStartedAtUtc = $SessionStartedAtUtc
$session.sessionCompletedAtUtc = if ($blockers.Count -eq 0) {
    if ([string]::IsNullOrWhiteSpace($SessionCompletedAtUtc)) { $now.ToString("o") } else { $SessionCompletedAtUtc }
}
else {
    ""
}
$session.executionState = if ($blockers.Count -eq 0) { "completed" } else { "blocked" }
$session.executionContext.changeTicketId = $certification.executionContext.changeTicketId
$session.executionContext.tenantScopeRef = $certification.executionContext.tenantScopeRef
$session.executionContext.executionWindowId = $certification.executionContext.executionWindowId
$session.executionContext.operatorId = $certification.executionContext.operatorId
$session.executionContext.approverId = $certification.executionContext.approverId
$session.executionContext.incidentRefs = @($certification.executionContext.incidentRefs)
$session.fallbackPosture.catalogSourceMode = $fallback.catalogSourceMode
$session.fallbackPosture.productionLike = [bool]$fallback.productionLike
$session.fallbackPosture.fallbackUsed = [bool]$fallback.fallbackUsed
$session.fallbackPosture.fallbackAuthorized = [bool]$fallback.fallbackAuthorized
$session.fallbackPosture.fallbackAuthorizationUri = [string]$fallback.fallbackAuthorizationUri
$session.fallbackPosture.fallbackDecision = [string]$fallback.fallbackDecision
$session.decisionInputs.requiredDrillsPresent = $requiredPresent
$session.decisionInputs.requiredDrillsCompleted = $requiredCompleted
$session.decisionInputs.exampleEvidencePresent = $examplePresent
$session.decisionInputs.staleEvidencePresent = $stalePresent
$session.decisionInputs.fallbackAccepted = $fallbackAccepted
$session.decisionInputs.operatorSignoffCompleted = $signoffCompleted
$session.waivers = @($waiverEntries)
$session.blockers = @($blockers | Sort-Object -Unique)

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$session | ConvertTo-Json -Depth 12 | Set-Content -Path $OutputPath -Encoding utf8

[ordered]@{
    sessionPath = $OutputPath
    sessionId = $session.sessionId
    executionState = $session.executionState
    blockers = $session.blockers
    staleDrills = @($staleDrills | Select-Object -Unique)
} | ConvertTo-Json -Depth 6
