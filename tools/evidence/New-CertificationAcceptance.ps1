param(
    [Parameter(Mandatory = $true)]
    [string]$CertificationRunPath,

    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

    [string]$OutputPath = "",
    [string]$AcceptedBy = "",
    [string]$ApprovedBy = "",
    [int]$FreshnessWindowDays = 14,
    [string[]]$WaiverNotes = @()
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

$repoRoot = Resolve-RepoRoot
$templatePath = Join-Path $repoRoot "docs/certification_acceptance.template.json"
if (-not (Test-Path $templatePath)) {
    throw "Missing certification acceptance template."
}

$resolvedCertificationRunPath = Resolve-ArtifactPath -Path $CertificationRunPath -RepoRoot $repoRoot
$resolvedSummaryPath = Resolve-ArtifactPath -Path $SummaryPath -RepoRoot $repoRoot
$resolvedBundlePath = Resolve-ArtifactPath -Path $BundlePath -RepoRoot $repoRoot

$certification = Read-JsonFile -Path $resolvedCertificationRunPath
$summary = Read-JsonFile -Path $resolvedSummaryPath
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
$waivers = @($WaiverNotes | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$exampleEvidencePresent = @($summary.exampleEvidenceKinds).Count -gt 0
$staleEvidencePresent = @($summary.staleEvidenceKinds).Count -gt 0
$requiredEvidenceComplete = @($summary.missingEvidenceKinds).Count -eq 0
$fallbackAccepted = -not ($summary.productionLike -and $summary.fallbackUsed -and $summary.fallbackDecision -ne "authorized_exception")
$reviewReady = $summary.reviewDecision -eq "ready_for_release_review" -and $summary.reviewState -eq "ready_for_review"
$signoff = $certification.signoff
$signoffPresent = -not [string]::IsNullOrWhiteSpace([string]$signoff.signoffUri)
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
if (-not $acceptedByPresent) {
    $blockers += "accepted_by_missing"
}
if (-not $approvedByPresent) {
    $blockers += "approved_by_missing"
}
foreach ($summaryBlocker in $summaryBlockers) {
    if ($blockers -notcontains $summaryBlocker) {
        $blockers += $summaryBlocker
    }
}

$nonWaivableBlockers = @($blockers | Where-Object { $_ -in @("example_or_dry_run_evidence", "fallback_authorization_gap", "operator_signoff_missing", "accepted_by_missing", "approved_by_missing") })
$acceptanceStatus = if ($blockers.Count -eq 0) {
    "accepted"
}
elseif ($waivers.Count -gt 0 -and $nonWaivableBlockers.Count -eq 0) {
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
$acceptance.releaseEvidenceBundle.path = $BundlePath
$acceptance.releaseEvidenceBundle.sha256 = $bundleDigest.sha256
$acceptance.releaseEvidenceBundle.artifactHashes = @($bundleDigest.artifactHashes)
$acceptance.fallbackPosture.catalogSourceMode = $summary.catalogSourceMode
$acceptance.fallbackPosture.productionLike = [bool]$summary.productionLike
$acceptance.fallbackPosture.fallbackUsed = [bool]$summary.fallbackUsed
$acceptance.fallbackPosture.fallbackAuthorized = [bool]$summary.fallbackAuthorized
$acceptance.fallbackPosture.fallbackAuthorizationUri = $summary.fallbackAuthorizationUri
$acceptance.fallbackPosture.fallbackDecision = $summary.fallbackDecision
$acceptance.signoff.operatorSignoffUri = $signoff.signoffUri
$acceptance.signoff.acceptedBy = $AcceptedBy
$acceptance.signoff.approvedBy = $ApprovedBy
$acceptance.decisionInputs.requiredEvidenceComplete = $requiredEvidenceComplete
$acceptance.decisionInputs.exampleEvidencePresent = $exampleEvidencePresent
$acceptance.decisionInputs.staleEvidencePresent = $staleEvidencePresent
$acceptance.decisionInputs.fallbackAccepted = $fallbackAccepted
$acceptance.decisionInputs.reviewReady = $reviewReady
$acceptance.decisionInputs.signoffPresent = $signoffPresent
$acceptance.decisionInputs.bundleHashCaptured = -not [string]::IsNullOrWhiteSpace([string]$bundleDigest.sha256)
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
