param(
    [Parameter(Mandatory = $true)]
    [string]$CertificationRunPath,

    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$ExecutionSessionPath,

    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

    [string]$OutputPath = "",
    [string]$AcceptedBy = "",
    [string]$ApprovedBy = "",
    [int]$FreshnessWindowDays = 14,
    [string[]]$WaiverNotes = @(),
    [string]$WaiverPath = ""
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

function Get-FileHashText {
    param([string]$Path)

    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-BundleDigest {
    param([string]$BundlePath)

    $artifactNames = @(
        "db-major-readiness-evidence-packet.json",
        "certification-evidence-manifest.json",
        "fallback-posture.json",
        "validation-results.json",
        "certification-review-summary.json"
    )

    $artifactHashes = @()
    foreach ($artifactName in $artifactNames) {
        $artifactPath = Join-Path $BundlePath $artifactName
        if (-not (Test-Path $artifactPath)) {
            throw "Missing bundle artifact required for acceptance hashing: $artifactPath"
        }

        $artifactHashes += [ordered]@{
            path = $artifactName
            sha256 = Get-FileHashText -Path $artifactPath
        }
    }

    $hashInput = ($artifactHashes | ForEach-Object { "$($_.path):$($_.sha256)" }) -join "`n"
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($hashInput)
        $hashBytes = $sha.ComputeHash($bytes)
        $bundleHash = -join ($hashBytes | ForEach-Object { $_.ToString("x2") })
    }
    finally {
        $sha.Dispose()
    }

    return [ordered]@{
        sha256 = $bundleHash
        artifactHashes = $artifactHashes
    }
}

function Normalize-WaiverEntries {
    param(
        [object]$WaiverDocument,
        [DateTimeOffset]$UtcNow
    )

    $entries = @()
    foreach ($waiver in @($WaiverDocument)) {
        if ($null -eq $waiver) {
            continue
        }

        $waiverId = [string]$waiver.waiverId
        if ([string]::IsNullOrWhiteSpace($waiverId)) {
            $waiverId = "waiver://generated/$([guid]::NewGuid().ToString('N'))"
        }

        $linkedDrills = @($waiver.linkedDrills | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        $status = "active"
        $expiresAtUtc = [string]$waiver.expiresAtUtc
        $expiry = [DateTimeOffset]::MinValue
        if ([string]::IsNullOrWhiteSpace([string]$waiver.authority) -or [string]::IsNullOrWhiteSpace([string]$waiver.reason) -or $linkedDrills.Count -eq 0) {
            $status = "invalid"
        }
        elseif (-not [DateTimeOffset]::TryParse($expiresAtUtc, [ref]$expiry)) {
            $status = "invalid"
        }
        elseif ($expiry -le $UtcNow) {
            $status = "expired"
        }

        $entries += [ordered]@{
            waiverId = $waiverId
            authority = [string]$waiver.authority
            scope = if ([string]::IsNullOrWhiteSpace([string]$waiver.scope)) { "drill" } else { [string]$waiver.scope }
            reason = [string]$waiver.reason
            expiresAtUtc = $expiresAtUtc
            linkedDrills = @($linkedDrills)
            nonWaivable = [bool]$waiver.nonWaivable
            status = $status
        }
    }

    return @($entries)
}

$repoRoot = Resolve-RepoRoot
$templatePath = Join-Path $repoRoot "docs/certification_acceptance.template.json"
if (-not (Test-Path $templatePath)) {
    throw "Missing certification acceptance template."
}

$resolvedCertificationRunPath = Resolve-ArtifactPath -Path $CertificationRunPath -RepoRoot $repoRoot
$resolvedSummaryPath = Resolve-ArtifactPath -Path $SummaryPath -RepoRoot $repoRoot
$resolvedExecutionSessionPath = Resolve-ArtifactPath -Path $ExecutionSessionPath -RepoRoot $repoRoot
$resolvedBundlePath = Resolve-ArtifactPath -Path $BundlePath -RepoRoot $repoRoot

$certification = Read-JsonFile -Path $resolvedCertificationRunPath
$summary = Read-JsonFile -Path $resolvedSummaryPath
$executionSession = Read-JsonFile -Path $resolvedExecutionSessionPath
$bundleDigest = Get-BundleDigest -BundlePath $resolvedBundlePath

if ($summary.releaseId -ne $certification.releaseId) {
    throw "Certification summary releaseId '$($summary.releaseId)' does not match certification run '$($certification.releaseId)'."
}

if ($summary.certificationRunId -ne $certification.certificationRunId) {
    throw "Certification summary run id '$($summary.certificationRunId)' does not match certification run '$($certification.certificationRunId)'."
}

if ($summary.environment -ne $certification.environment) {
    throw "Certification summary environment '$($summary.environment)' does not match certification run '$($certification.environment)'."
}

if ($executionSession.releaseId -ne $certification.releaseId) {
    throw "Execution session releaseId '$($executionSession.releaseId)' does not match certification run '$($certification.releaseId)'."
}

if ($executionSession.certificationRunId -ne $certification.certificationRunId) {
    throw "Execution session run id '$($executionSession.certificationRunId)' does not match certification run '$($certification.certificationRunId)'."
}

if ($executionSession.environment -ne $certification.environment) {
    throw "Execution session environment '$($executionSession.environment)' does not match certification run '$($certification.environment)'."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $resolvedBundlePath "certification-acceptance.json"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repoRoot $OutputPath
}

$now = (Get-Date).ToUniversalTime()
$expiresAt = $now.AddDays($FreshnessWindowDays)
$acceptance = Get-Content $templatePath -Raw | ConvertFrom-Json
$summaryBlockers = @($summary.blockers)
$executionBlockers = @($executionSession.blockers)
$waivers = @()
$waiverNotes = @($WaiverNotes | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
foreach ($waiverNote in $waiverNotes) {
    $waivers += [ordered]@{
        waiverId = "waiver://note/$([guid]::NewGuid().ToString('N'))"
        authority = "release_authority"
        scope = "acceptance"
        reason = [string]$waiverNote
        expiresAtUtc = $now.AddDays(7).ToString("o")
        linkedDrills = @()
        nonWaivable = $false
        status = "active"
    }
}
if (-not [string]::IsNullOrWhiteSpace($WaiverPath)) {
    $resolvedWaiverPath = Resolve-ArtifactPath -Path $WaiverPath -RepoRoot $repoRoot
    $waivers += Normalize-WaiverEntries -WaiverDocument (Read-JsonFile -Path $resolvedWaiverPath) -UtcNow $now
}
$invalidWaiverPresent = @($waivers | Where-Object { $_.status -ne "active" }).Count -gt 0
$activeWaivers = @($waivers | Where-Object { $_.status -eq "active" })
$exampleEvidencePresent = @($summary.exampleEvidenceKinds).Count -gt 0
$staleEvidencePresent = @($summary.staleEvidenceKinds).Count -gt 0 -or [bool]$executionSession.decisionInputs.staleEvidencePresent
$requiredEvidenceComplete = @($summary.missingEvidenceKinds).Count -eq 0
$fallbackAccepted = -not ($summary.productionLike -and $summary.fallbackUsed -and $summary.fallbackDecision -ne "authorized_exception")
$reviewReady = $summary.reviewDecision -eq "ready_for_release_review" -and $summary.reviewState -eq "ready_for_review"
$signoffEntry = $executionSession.drillLedger | Where-Object { $_.drillKind -eq "operator_signoff" } | Select-Object -First 1
$signoff = $certification.signoff
$signoffUri = if ($null -ne $signoffEntry -and -not [string]::IsNullOrWhiteSpace([string]$signoffEntry.evidenceUri)) { [string]$signoffEntry.evidenceUri } else { [string]$signoff.signoffUri }
$signoffPresent = -not [string]::IsNullOrWhiteSpace($signoffUri)
$executionSessionComplete = $executionSession.executionState -eq "completed" -and [bool]$executionSession.decisionInputs.requiredDrillsCompleted -and [bool]$executionSession.decisionInputs.fallbackAccepted -and [bool]$executionSession.decisionInputs.operatorSignoffCompleted -and -not [bool]$executionSession.decisionInputs.exampleEvidencePresent -and -not [bool]$executionSession.decisionInputs.staleEvidencePresent -and $executionBlockers.Count -eq 0
$trustedEvidenceReady = [bool]$executionSession.decisionInputs.trustedEvidenceReady
$trustedEvidencePolicySatisfied = [bool]$executionSession.decisionInputs.trustedEvidencePolicySatisfied
$acceptedByPresent = -not [string]::IsNullOrWhiteSpace($AcceptedBy)
$approvedByPresent = -not [string]::IsNullOrWhiteSpace($ApprovedBy)

$blockers = @()
if (-not $requiredEvidenceComplete) {
    $blockers += "missing_evidence"
}
if ($exampleEvidencePresent -or $summary.reviewState -eq "dry-run") {
    $blockers += "example_or_dry_run_evidence"
}
if ($staleEvidencePresent) {
    $blockers += "stale_evidence"
}
if (-not $fallbackAccepted) {
    $blockers += "fallback_authorization_gap"
}
if (-not $reviewReady) {
    $blockers += "review_not_ready"
}
if (-not $signoffPresent) {
    $blockers += "operator_signoff_missing"
}
if (-not $executionSessionComplete) {
    $blockers += "execution_session_incomplete"
}
if (-not $trustedEvidenceReady) {
    $blockers += "trusted_evidence_incomplete"
}
if (-not $trustedEvidencePolicySatisfied) {
    $blockers += "trusted_evidence_policy_violation"
}
if ($invalidWaiverPresent) {
    $blockers += "invalid_waiver"
}
if (-not $acceptedByPresent) {
    $blockers += "accepted_by_missing"
}
if (-not $approvedByPresent) {
    $blockers += "approved_by_missing"
}
foreach ($executionBlocker in $executionBlockers) {
    if ($blockers -notcontains $executionBlocker) {
        $blockers += $executionBlocker
    }
}
foreach ($summaryBlocker in $summaryBlockers) {
    if ($blockers -notcontains $summaryBlocker) {
        $blockers += $summaryBlocker
    }
}

$nonWaivableBlockers = @($blockers | Where-Object { $_ -in @("example_or_dry_run_evidence", "fallback_authorization_gap", "operator_signoff_missing", "accepted_by_missing", "approved_by_missing", "trusted_evidence_incomplete", "trusted_evidence_policy_violation", "invalid_waiver") })
$acceptanceStatus = if ($blockers.Count -eq 0) {
    "accepted"
}
elseif ($activeWaivers.Count -gt 0 -and $nonWaivableBlockers.Count -eq 0) {
    "waived"
}
else {
    "blocked"
}

$acceptance.releaseId = $certification.releaseId
$acceptance.certificationRunId = $certification.certificationRunId
$acceptance.environment = $certification.environment
$acceptance.acceptanceStatus = $acceptanceStatus
$acceptance.acceptedBy = $AcceptedBy
$acceptance.approvedBy = $ApprovedBy
$acceptance.acceptedAtUtc = $now.ToString("o")
$acceptance.expiresAtUtc = $expiresAt.ToString("o")
$acceptance.freshnessWindowDays = $FreshnessWindowDays
$acceptance.certificationRunManifest.path = $CertificationRunPath
$acceptance.certificationRunManifest.sha256 = Get-FileHashText -Path $resolvedCertificationRunPath
$acceptance.certificationReviewSummary.path = $SummaryPath
$acceptance.certificationReviewSummary.sha256 = Get-FileHashText -Path $resolvedSummaryPath
$acceptance.certificationReviewSummary.reviewDecision = $summary.reviewDecision
$acceptance.certificationReviewSummary.reviewState = $summary.reviewState
$acceptance.certificationReviewSummary.reviewGateDecision = $summary.reviewGateDecision
$acceptance.certificationExecutionSession.path = $ExecutionSessionPath
$acceptance.certificationExecutionSession.sha256 = Get-FileHashText -Path $resolvedExecutionSessionPath
$acceptance.certificationExecutionSession.sessionId = [string]$executionSession.sessionId
$acceptance.certificationExecutionSession.executionState = [string]$executionSession.executionState
$acceptance.certificationExecutionSession.goNoGo = $(if ($executionSessionComplete) { "ready_for_acceptance" } else { "blocked" })
$acceptance.releaseEvidenceBundle.path = $BundlePath
$acceptance.releaseEvidenceBundle.sha256 = $bundleDigest.sha256
$acceptance.releaseEvidenceBundle.artifactHashes = @($bundleDigest.artifactHashes)
$acceptance.fallbackPosture.catalogSourceMode = $summary.catalogSourceMode
$acceptance.fallbackPosture.productionLike = [bool]$summary.productionLike
$acceptance.fallbackPosture.fallbackUsed = [bool]$summary.fallbackUsed
$acceptance.fallbackPosture.fallbackAuthorized = [bool]$summary.fallbackAuthorized
$acceptance.fallbackPosture.fallbackAuthorizationUri = $summary.fallbackAuthorizationUri
$acceptance.fallbackPosture.fallbackDecision = $summary.fallbackDecision
$acceptance.signoff.operatorSignoffUri = $signoffUri
$acceptance.signoff.acceptedBy = $AcceptedBy
$acceptance.signoff.approvedBy = $ApprovedBy
$acceptance.decisionInputs.requiredEvidenceComplete = $requiredEvidenceComplete
$acceptance.decisionInputs.exampleEvidencePresent = $exampleEvidencePresent
$acceptance.decisionInputs.staleEvidencePresent = $staleEvidencePresent
$acceptance.decisionInputs.fallbackAccepted = $fallbackAccepted
$acceptance.decisionInputs.reviewReady = $reviewReady
$acceptance.decisionInputs.signoffPresent = $signoffPresent
$acceptance.decisionInputs.bundleHashCaptured = -not [string]::IsNullOrWhiteSpace([string]$bundleDigest.sha256)
$acceptance.decisionInputs.executionSessionComplete = $executionSessionComplete
$acceptance.decisionInputs.trustedEvidenceReady = $trustedEvidenceReady
$acceptance.decisionInputs.trustedEvidencePolicySatisfied = $trustedEvidencePolicySatisfied
$acceptance.waivers = @($waivers)
$acceptance.blockers = @($blockers | Sort-Object -Unique)

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$acceptance | ConvertTo-Json -Depth 12 | Set-Content -Path $OutputPath -Encoding utf8

[ordered]@{
    acceptancePath = $OutputPath
    acceptanceStatus = $acceptance.acceptanceStatus
    expiresAtUtc = $acceptance.expiresAtUtc
    blockers = $acceptance.blockers
} | ConvertTo-Json -Depth 6
