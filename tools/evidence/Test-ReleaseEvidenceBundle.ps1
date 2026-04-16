param(
    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

    [switch]$AllowMissingEvidence
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Missing required evidence artifact: $Path"
    }

    return Get-Content $Path -Raw | ConvertFrom-Json
}

$packetPath = Join-Path $BundlePath "db-major-readiness-evidence-packet.json"
$certificationPath = Join-Path $BundlePath "certification-evidence-manifest.json"
$fallbackPath = Join-Path $BundlePath "fallback-posture.json"
$validationPath = Join-Path $BundlePath "validation-results.json"

$packet = Read-JsonFile -Path $packetPath
$certification = Read-JsonFile -Path $certificationPath
$fallback = Read-JsonFile -Path $fallbackPath
$validation = Read-JsonFile -Path $validationPath

$errors = New-Object System.Collections.Generic.List[string]

if ([string]::IsNullOrWhiteSpace($packet.releaseId)) {
    $errors.Add("Evidence packet releaseId is required.")
}

if ([string]::IsNullOrWhiteSpace($packet.environment)) {
    $errors.Add("Evidence packet environment is required.")
}

if ([string]::IsNullOrWhiteSpace($packet.compatibilityInventory.inventorySha256)) {
    $errors.Add("Compatibility inventory SHA-256 must be captured.")
}

if ($packet.releaseAttachments.staticInventoryUri -ne "docs/compatibility_inventory.json") {
    $errors.Add("Evidence packet must reference docs/compatibility_inventory.json.")
}

if ($certification.requiredEvidence.Count -eq 0) {
    $errors.Add("Certification manifest must list required evidence kinds.")
}

if (-not $AllowMissingEvidence) {
    $missingEvidence = @($certification.requiredEvidence | Where-Object { $_.status -eq "missing" })
    if ($missingEvidence.Count -gt 0) {
        $errors.Add("Missing certification evidence: $($missingEvidence.evidenceKind -join ', ')")
    }
}

if ($fallback.productionLike -and $fallback.fallbackUsed -and -not $fallback.fallbackDisciplinePassed) {
    $errors.Add("Production-like fallback was used without authorization evidence.")
}

if (-not $validation.bundleGenerated) {
    $errors.Add("Validation artifact must confirm bundle generation.")
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "Release evidence bundle validated: $BundlePath"
