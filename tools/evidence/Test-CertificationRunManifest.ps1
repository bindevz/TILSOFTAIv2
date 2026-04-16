param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [switch]$AllowMissingEvidence,
    [switch]$AllowExampleEvidence
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$requiredKinds = @(
    "runbook_execution",
    "preview_failure_drill",
    "version_conflict_drill",
    "duplicate_submit_drill",
    "sql_apply_outage_drill",
    "fallback_risk_drill",
    "operator_signoff"
)

function Test-EvidenceUri {
    param([string]$Uri)

    if ([string]::IsNullOrWhiteSpace($Uri)) {
        return $false
    }

    return $Uri -match "^(artifact|https|ticket|incident)://.+" -or $Uri -match "^[A-Z]+-[0-9]+$"
}

if (-not (Test-Path $ManifestPath)) {
    throw "Missing certification run manifest: $ManifestPath"
}

$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$errors = New-Object System.Collections.Generic.List[string]

if ($manifest.schemaVersion -ne 1) {
    $errors.Add("Certification manifest schemaVersion must be 1.")
}

foreach ($field in @("releaseId", "certificationRunId", "environment", "generatedAtUtc", "reviewState")) {
    if ([string]::IsNullOrWhiteSpace([string]$manifest.$field)) {
        $errors.Add("Certification manifest field '$field' is required.")
    }
}

$actualKinds = @($manifest.requiredEvidence | ForEach-Object { $_.evidenceKind })
foreach ($kind in $requiredKinds) {
    if ($actualKinds -notcontains $kind) {
        $errors.Add("Missing required evidence kind '$kind'.")
    }
}

foreach ($item in $manifest.requiredEvidence) {
    $kind = [string]$item.evidenceKind
    $uri = [string]$item.evidenceUri
    $status = [string]$item.status

    if ($item.required -and [string]::IsNullOrWhiteSpace($uri) -and -not $AllowMissingEvidence) {
        $errors.Add("Required evidence '$kind' is missing.")
    }

    if (-not [string]::IsNullOrWhiteSpace($uri) -and -not (Test-EvidenceUri -Uri $uri)) {
        $errors.Add("Evidence '$kind' has an unsupported URI or identifier: $uri")
    }

    if (($status -eq "example" -or $status -eq "blocked_example" -or $uri -match "example") -and -not $AllowExampleEvidence) {
        $errors.Add("Evidence '$kind' uses example evidence and cannot satisfy live certification.")
    }
}

$fallback = $manifest.fallbackPosture
if ($fallback.productionLike -and $fallback.fallbackUsed -and $fallback.fallbackDecision -ne "authorized_exception") {
    $errors.Add("Production-like fallback requires authorized_exception fallbackDecision.")
}

if ($fallback.productionLike -and $fallback.fallbackUsed -and [string]::IsNullOrWhiteSpace([string]$fallback.fallbackAuthorizationUri)) {
    $errors.Add("Production-like fallback requires fallbackAuthorizationUri.")
}

$signoff = $manifest.signoff
if ($signoff.required -and [string]::IsNullOrWhiteSpace([string]$signoff.signoffUri) -and -not $AllowMissingEvidence) {
    $errors.Add("Operator signoff evidence is required.")
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "Certification run manifest validated: $ManifestPath"
