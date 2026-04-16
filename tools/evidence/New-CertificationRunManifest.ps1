param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseId,

    [Parameter(Mandatory = $true)]
    [string]$Environment,

    [string]$CertificationRunId = "",
    [string]$GeneratedBy = $env:USERNAME,
    [string]$OutputPath = "",
    [string]$EvidenceRefsPath = "",
    [ValidateSet("platform", "mixed", "bootstrap_only", "empty", "unknown")]
    [string]$CatalogSourceMode = "unknown",
    [switch]$FallbackAuthorized,
    [string]$FallbackAuthorizationUri = "",
    [int]$FreshnessWindowDays = 30,
    [switch]$AllowExampleEvidence
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

function Read-EvidenceRefs {
    param([string]$Path)

    $refs = @{}
    if ($Path -and (Test-Path $Path)) {
        $raw = Get-Content $Path -Raw | ConvertFrom-Json
        foreach ($property in $raw.PSObject.Properties) {
            $refs[$property.Name] = [string]$property.Value
        }
    }

    return $refs
}

$repoRoot = Resolve-RepoRoot
$templatePath = Join-Path $repoRoot "docs/certification_run_manifest.template.json"
if (-not (Test-Path $templatePath)) {
    throw "Missing certification run manifest template."
}

$now = (Get-Date).ToUniversalTime().ToString("o")
if ([string]::IsNullOrWhiteSpace($CertificationRunId)) {
    $CertificationRunId = "$ReleaseId-$Environment-certification"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Join-Path $repoRoot "release-evidence") (Join-Path $ReleaseId "certification-run-manifest.json")
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repoRoot $OutputPath
}

$manifest = Get-Content $templatePath -Raw | ConvertFrom-Json
$refs = Read-EvidenceRefs -Path $EvidenceRefsPath
$productionLike = $Environment -in @("prod", "production", "staging")
$fallbackUsed = $CatalogSourceMode -in @("mixed", "bootstrap_only")
$fallbackDecision = if ((-not $productionLike) -or (-not $fallbackUsed)) {
    "accepted"
}
elseif ($FallbackAuthorized -and -not [string]::IsNullOrWhiteSpace($FallbackAuthorizationUri)) {
    "authorized_exception"
}
else {
    "blocked"
}

$manifest.releaseId = $ReleaseId
$manifest.certificationRunId = $CertificationRunId
$manifest.environment = $Environment
$manifest.generatedAtUtc = $now
$manifest.generatedBy = $GeneratedBy
$manifest.freshnessWindowDays = $FreshnessWindowDays
$manifest.fallbackPosture.catalogSourceMode = $CatalogSourceMode
$manifest.fallbackPosture.productionLike = $productionLike
$manifest.fallbackPosture.fallbackUsed = $fallbackUsed
$manifest.fallbackPosture.fallbackAuthorized = [bool]$FallbackAuthorized
$manifest.fallbackPosture.fallbackAuthorizationUri = $FallbackAuthorizationUri
$manifest.fallbackPosture.fallbackDecision = $fallbackDecision

$missingCount = 0
$exampleCount = 0
foreach ($item in $manifest.requiredEvidence) {
    $kind = $item.evidenceKind
    $uri = ""
    if ($refs.ContainsKey($kind)) {
        $uri = $refs[$kind]
    }

    $item.freshnessWindowDays = if ($kind -eq "operator_signoff") { 14 } else { $FreshnessWindowDays }
    $item.evidenceUri = $uri
    $item.collectedAtUtc = $(if ([string]::IsNullOrWhiteSpace($uri)) { "" } else { $now })

    if ([string]::IsNullOrWhiteSpace($uri)) {
        $item.status = "missing"
        $missingCount++
    }
    elseif ($uri -match "/example/" -or $uri -match "example") {
        $item.status = $(if ($AllowExampleEvidence) { "example" } else { "blocked_example" })
        $exampleCount++
    }
    else {
        $item.status = "referenced"
    }
}

$signoff = $manifest.requiredEvidence | Where-Object { $_.evidenceKind -eq "operator_signoff" } | Select-Object -First 1
$manifest.signoff.signoffUri = $signoff.evidenceUri
$manifest.reviewState = if ($missingCount -gt 0 -or $fallbackDecision -eq "blocked" -or ($exampleCount -gt 0 -and -not $AllowExampleEvidence)) {
    "blocked"
}
elseif ($exampleCount -gt 0) {
    "dry-run"
}
else {
    "ready_for_review"
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$manifest | ConvertTo-Json -Depth 12 | Set-Content -Path $OutputPath -Encoding utf8

[ordered]@{
    manifestPath = $OutputPath
    reviewState = $manifest.reviewState
    fallbackDecision = $fallbackDecision
    missingEvidenceCount = $missingCount
    exampleEvidenceCount = $exampleCount
} | ConvertTo-Json -Depth 4
