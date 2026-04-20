param(
    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [switch]$AllowBlocked,
    [switch]$AllowExampleEvidence
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-JsonProperty {
    param(
        [object]$InputObject,
        [string]$Name
    )

    return $null -ne $InputObject -and $InputObject.PSObject.Properties.Name -contains $Name
}

if (-not (Test-Path $SummaryPath)) {
    throw "Missing certification review summary: $SummaryPath"
}

$summary = Get-Content $SummaryPath -Raw | ConvertFrom-Json
$errors = New-Object System.Collections.Generic.List[string]

if ($summary.schemaVersion -ne 1) {
    $errors.Add("Certification review summary schemaVersion must be 1.")
}

if ($summary.summaryType -ne "certification-review-summary") {
    $errors.Add("Certification review summary summaryType must be certification-review-summary.")
}

foreach ($field in @("releaseId", "certificationRunId", "environment", "generatedAtUtc", "reviewDecision", "reviewState", "reviewGateDecision", "fallbackDecision")) {
    if ([string]::IsNullOrWhiteSpace([string]$summary.$field)) {
        $errors.Add("Certification review summary field '$field' is required.")
    }
}

if (-not (Test-JsonProperty -InputObject $summary -Name "executionContext")) {
    $errors.Add("Certification review summary must include executionContext.")
}

foreach ($field in @("catalogSourceMode", "productionLike", "fallbackUsed", "fallbackAuthorized", "fallbackAuthorizationUri", "acceptedForReleaseReview", "blockers")) {
    if (-not (Test-JsonProperty -InputObject $summary -Name $field)) {
        $errors.Add("Certification review summary field '$field' is required.")
    }
}

if ($summary.reviewDecision -eq "ready_for_release_review" -and -not [bool]$summary.acceptedForReleaseReview) {
    $errors.Add("Certification summary cannot be ready_for_release_review when acceptedForReleaseReview is false.")
}

if (-not $AllowBlocked -and $summary.reviewDecision -ne "ready_for_release_review") {
    $errors.Add("Certification review summary is blocked: $($summary.blockers -join ', ')")
}

if (-not $AllowExampleEvidence -and @($summary.exampleEvidenceKinds).Count -gt 0) {
    $errors.Add("Certification review summary contains example evidence and cannot satisfy live certification.")
}

if ($summary.productionLike -and $summary.fallbackUsed -and $summary.fallbackDecision -ne "authorized_exception") {
    $errors.Add("Production-like fallback must be authorized_exception in certification review summary.")
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "Certification review summary validated: $SummaryPath"
