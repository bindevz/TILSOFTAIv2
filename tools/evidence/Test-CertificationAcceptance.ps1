param(
    [Parameter(Mandatory = $true)]
    [string]$AcceptancePath,

    [switch]$AllowWaived,
    [switch]$AllowExpired
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
        [string]$RepoRoot,
        [string]$AcceptanceDirectory
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    $repoPath = Join-Path $RepoRoot $Path
    if (Test-Path $repoPath) {
        return $repoPath
    }

    return Join-Path $AcceptanceDirectory $Path
}

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Missing required artifact: $Path"
    }

    return Get-Content $Path -Raw | ConvertFrom-Json
}

function Test-JsonProperty {
    param(
        [object]$InputObject,
        [string]$Name
    )

    return $null -ne $InputObject -and $InputObject.PSObject.Properties.Name -contains $Name
}

function Get-FileHashText {
    param([string]$Path)

    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-DateTimeOffset {
    param([string]$Value)

    $parsed = [DateTimeOffset]::MinValue
    return [DateTimeOffset]::TryParse($Value, [ref]$parsed)
}

$repoRoot = Resolve-RepoRoot
$resolvedAcceptancePath = if ([System.IO.Path]::IsPathRooted($AcceptancePath)) { $AcceptancePath } else { Join-Path $repoRoot $AcceptancePath }
if (-not (Test-Path $resolvedAcceptancePath)) {
    throw "Missing certification acceptance artifact: $AcceptancePath"
}

$acceptanceDirectory = Split-Path -Parent $resolvedAcceptancePath
$acceptance = Read-JsonFile -Path $resolvedAcceptancePath
$errors = New-Object System.Collections.Generic.List[string]
$allowedStatuses = @("draft", "accepted", "blocked", "waived", "expired")
$nonLiveStatuses = @("draft", "blocked", "expired")
$nonWaivableBlockers = @("example_or_dry_run_evidence", "fallback_authorization_gap", "operator_signoff_missing", "accepted_by_missing", "approved_by_missing", "execution_session_incomplete")

if ($acceptance.schemaVersion -ne 1) {
    $errors.Add("Certification acceptance schemaVersion must be 1.")
}

if ($acceptance.artifactType -ne "live-certification-acceptance") {
    $errors.Add("Certification acceptance artifactType must be live-certification-acceptance.")
}

foreach ($field in @("releaseId", "certificationRunId", "environment", "acceptanceStatus", "acceptedBy", "approvedBy", "acceptedAtUtc", "expiresAtUtc")) {
    if ([string]::IsNullOrWhiteSpace([string]$acceptance.$field)) {
        $errors.Add("Certification acceptance field '$field' is required.")
    }
}

if ($allowedStatuses -notcontains [string]$acceptance.acceptanceStatus) {
    $errors.Add("Certification acceptanceStatus '$($acceptance.acceptanceStatus)' is not supported.")
}

if ($nonLiveStatuses -contains [string]$acceptance.acceptanceStatus) {
    $errors.Add("Certification acceptanceStatus '$($acceptance.acceptanceStatus)' is not accepted live certification.")
}

if ($acceptance.acceptanceStatus -eq "waived" -and -not $AllowWaived) {
    $errors.Add("Waived certification acceptance requires -AllowWaived.")
}

if ($acceptance.acceptanceStatus -eq "accepted" -and @($acceptance.blockers).Count -gt 0) {
    $errors.Add("Accepted certification cannot contain blockers.")
}

if (@($acceptance.blockers | Where-Object { $nonWaivableBlockers -contains $_ }).Count -gt 0) {
    $errors.Add("Certification acceptance contains non-waivable live-certification blockers.")
}

if (@($acceptance.waivers).Count -gt 0 -and $acceptance.acceptanceStatus -ne "waived") {
    $errors.Add("Waiver notes require acceptanceStatus waived.")
}

foreach ($field in @("certificationRunManifest", "certificationReviewSummary", "certificationExecutionSession", "releaseEvidenceBundle", "fallbackPosture", "signoff", "decisionInputs")) {
    if (-not (Test-JsonProperty -InputObject $acceptance -Name $field)) {
        $errors.Add("Certification acceptance must include '$field'.")
    }
}

foreach ($field in @("path", "sha256", "sessionId", "executionState", "goNoGo")) {
    if (-not (Test-JsonProperty -InputObject $acceptance.certificationExecutionSession -Name $field)) {
        $errors.Add("Certification acceptance certificationExecutionSession field '$field' is required.")
    }
}

if (-not (Test-DateTimeOffset -Value ([string]$acceptance.acceptedAtUtc))) {
    $errors.Add("acceptedAtUtc must be a valid timestamp.")
}

$expiresAt = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParse([string]$acceptance.expiresAtUtc, [ref]$expiresAt)) {
    $errors.Add("expiresAtUtc must be a valid timestamp.")
}
elseif ($expiresAt -lt [DateTimeOffset]::UtcNow -and -not $AllowExpired) {
    $errors.Add("Certification acceptance is expired.")
}

if ([int]$acceptance.freshnessWindowDays -le 0) {
    $errors.Add("freshnessWindowDays must be positive.")
}

$manifestPath = Resolve-ArtifactPath -Path ([string]$acceptance.certificationRunManifest.path) -RepoRoot $repoRoot -AcceptanceDirectory $acceptanceDirectory
$summaryPath = Resolve-ArtifactPath -Path ([string]$acceptance.certificationReviewSummary.path) -RepoRoot $repoRoot -AcceptanceDirectory $acceptanceDirectory
$executionSessionPath = Resolve-ArtifactPath -Path ([string]$acceptance.certificationExecutionSession.path) -RepoRoot $repoRoot -AcceptanceDirectory $acceptanceDirectory
$bundlePath = Resolve-ArtifactPath -Path ([string]$acceptance.releaseEvidenceBundle.path) -RepoRoot $repoRoot -AcceptanceDirectory $acceptanceDirectory

$manifest = Read-JsonFile -Path $manifestPath
$summary = Read-JsonFile -Path $summaryPath
$executionSession = Read-JsonFile -Path $executionSessionPath

if ((Get-FileHashText -Path $manifestPath) -ne [string]$acceptance.certificationRunManifest.sha256) {
    $errors.Add("Certification run manifest SHA-256 does not match acceptance artifact.")
}

if ((Get-FileHashText -Path $summaryPath) -ne [string]$acceptance.certificationReviewSummary.sha256) {
    $errors.Add("Certification review summary SHA-256 does not match acceptance artifact.")
}

if ((Get-FileHashText -Path $executionSessionPath) -ne [string]$acceptance.certificationExecutionSession.sha256) {
    $errors.Add("Certification execution session SHA-256 does not match acceptance artifact.")
}

foreach ($artifactHash in $acceptance.releaseEvidenceBundle.artifactHashes) {
    $artifactPath = Join-Path $bundlePath ([string]$artifactHash.path)
    if (-not (Test-Path $artifactPath)) {
        $errors.Add("Acceptance references missing bundle artifact '$($artifactHash.path)'.")
    }
    elseif ((Get-FileHashText -Path $artifactPath) -ne [string]$artifactHash.sha256) {
        $errors.Add("Bundle artifact hash mismatch for '$($artifactHash.path)'.")
    }
}

if ($manifest.releaseId -ne $acceptance.releaseId -or $summary.releaseId -ne $acceptance.releaseId) {
    $errors.Add("Acceptance releaseId must match certification run and summary.")
}

if ($manifest.certificationRunId -ne $acceptance.certificationRunId -or $summary.certificationRunId -ne $acceptance.certificationRunId) {
    $errors.Add("Acceptance certificationRunId must match certification run and summary.")
}

if ($manifest.environment -ne $acceptance.environment -or $summary.environment -ne $acceptance.environment) {
    $errors.Add("Acceptance environment must match certification run and summary.")
}

if ($executionSession.releaseId -ne $acceptance.releaseId -or $executionSession.certificationRunId -ne $acceptance.certificationRunId -or $executionSession.environment -ne $acceptance.environment) {
    $errors.Add("Acceptance must match certification execution session release/run/environment.")
}

if ([string]$acceptance.certificationExecutionSession.sessionId -ne [string]$executionSession.sessionId) {
    $errors.Add("Acceptance execution session id does not match referenced session artifact.")
}

if ([string]$acceptance.certificationExecutionSession.executionState -ne [string]$executionSession.executionState) {
    $errors.Add("Acceptance execution session state does not match referenced session artifact.")
}

if ($summary.reviewDecision -ne "ready_for_release_review" -or $summary.reviewState -ne "ready_for_review") {
    $errors.Add("Acceptance requires a ready_for_release_review certification summary.")
}

if ($manifest.reviewState -eq "dry-run" -or @($summary.exampleEvidenceKinds).Count -gt 0 -or [bool]$acceptance.decisionInputs.exampleEvidencePresent) {
    $errors.Add("Dry-run/example evidence cannot be accepted as live certification.")
}

if (@($summary.missingEvidenceKinds).Count -gt 0 -or -not [bool]$acceptance.decisionInputs.requiredEvidenceComplete) {
    $errors.Add("Acceptance requires complete certification evidence.")
}

if (@($summary.staleEvidenceKinds).Count -gt 0 -or [bool]$acceptance.decisionInputs.staleEvidencePresent) {
    $errors.Add("Acceptance cannot use stale certification evidence.")
}

if ($executionSession.executionState -ne "completed" -or -not [bool]$executionSession.decisionInputs.requiredDrillsCompleted -or -not [bool]$executionSession.decisionInputs.operatorSignoffCompleted -or [bool]$executionSession.decisionInputs.exampleEvidencePresent -or [bool]$executionSession.decisionInputs.staleEvidencePresent -or @($executionSession.blockers).Count -gt 0) {
    $errors.Add("Acceptance requires a completed certification execution session with all required drills satisfied.")
}

if ($summary.productionLike -and $summary.fallbackUsed -and $summary.fallbackDecision -ne "authorized_exception") {
    $errors.Add("Production-like fallback cannot be accepted without authorized_exception.")
}

if (-not [bool]$acceptance.decisionInputs.signoffPresent -or [string]::IsNullOrWhiteSpace([string]$acceptance.signoff.operatorSignoffUri)) {
    $errors.Add("Acceptance requires operator signoff evidence.")
}

if (-not [bool]$acceptance.decisionInputs.bundleHashCaptured -or [string]::IsNullOrWhiteSpace([string]$acceptance.releaseEvidenceBundle.sha256)) {
    $errors.Add("Acceptance requires a captured release evidence bundle hash.")
}

if (-not [bool]$acceptance.decisionInputs.executionSessionComplete) {
    $errors.Add("Acceptance requires decisionInputs.executionSessionComplete to be true.")
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "Certification acceptance validated: $AcceptancePath"
