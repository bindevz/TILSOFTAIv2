param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseId,

    [Parameter(Mandatory = $true)]
    [string]$Environment,

    [string]$OutputRoot = "release-evidence",
    [string]$GeneratedBy = $env:USERNAME,
    [string]$WindowStartUtc = "",
    [string]$WindowEndUtc = "",
    [ValidateSet("platform", "mixed", "bootstrap_only", "empty", "unknown")]
    [string]$CatalogSourceMode = "unknown",
    [switch]$FallbackAuthorized,
    [string]$FallbackAuthorizationUri = "",
    [string]$EvidenceRefsPath = "",
    [string]$UsageSummaryUri = "",
    [string]$RetirementReadinessUri = "",
    [string]$RollbackPlanUri = "",
    [string]$OperatorCommunicationUri = "",
    [string]$ValidationResultsUri = ""
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

    $requiredKinds = @(
        "runbook_execution",
        "preview_failure_drill",
        "version_conflict_drill",
        "duplicate_submit_drill",
        "sql_apply_outage_drill",
        "fallback_risk_drill",
        "operator_signoff"
    )

    $refs = @{}
    if ($Path -and (Test-Path $Path)) {
        $raw = Get-Content $Path -Raw | ConvertFrom-Json
        foreach ($property in $raw.PSObject.Properties) {
            $refs[$property.Name] = [string]$property.Value
        }
    }

    $items = @()
    foreach ($kind in $requiredKinds) {
        $uri = ""
        if ($refs.ContainsKey($kind)) {
            $uri = $refs[$kind]
        }

        $items += [ordered]@{
            evidenceKind = $kind
            status = $(if ([string]::IsNullOrWhiteSpace($uri)) { "missing" } else { "referenced" })
            evidenceUri = $uri
        }
    }

    return $items
}

$repoRoot = Resolve-RepoRoot
$now = (Get-Date).ToUniversalTime().ToString("o")
if ([string]::IsNullOrWhiteSpace($WindowEndUtc)) {
    $WindowEndUtc = $now
}

$bundlePath = Join-Path (Join-Path $repoRoot $OutputRoot) $ReleaseId
New-Item -ItemType Directory -Force -Path $bundlePath | Out-Null

$inventoryRelativePath = "docs/compatibility_inventory.json"
$inventoryPath = Join-Path $repoRoot $inventoryRelativePath
$templatePath = Join-Path $repoRoot "docs/db_major_readiness_evidence_packet.template.json"

if (-not (Test-Path $inventoryPath)) {
    throw "Missing compatibility inventory: $inventoryRelativePath"
}

if (-not (Test-Path $templatePath)) {
    throw "Missing DB-major readiness evidence packet template."
}

$inventory = Get-Content $inventoryPath -Raw | ConvertFrom-Json
$inventoryHash = (Get-FileHash -Path $inventoryPath -Algorithm SHA256).Hash.ToLowerInvariant()
$packet = Get-Content $templatePath -Raw | ConvertFrom-Json
$fallbackUsed = $CatalogSourceMode -in @("mixed", "bootstrap_only")
$productionLike = $Environment -in @("prod", "production", "staging")

$packet.releaseId = $ReleaseId
$packet.environment = $Environment
$packet.generatedAtUtc = $now
$packet.generatedBy = $GeneratedBy
$packet.compatibilityInventory.inventoryVersion = $inventory.inventoryVersion
$packet.compatibilityInventory.inventorySha256 = $inventoryHash
$packet.telemetryWindow.windowStartUtc = $WindowStartUtc
$packet.telemetryWindow.windowEndUtc = $WindowEndUtc
$packet.releaseAttachments.usageSummaryQueryOutputUri = $UsageSummaryUri
$packet.releaseAttachments.retirementReadinessQueryOutputUri = $RetirementReadinessUri
$packet.releaseAttachments.staticInventoryUri = $inventoryRelativePath
$packet.releaseAttachments.fallbackPostureUri = "fallback-posture.json"
$packet.releaseAttachments.rollbackPlanUri = $RollbackPlanUri
$packet.releaseAttachments.operatorCommunicationUri = $OperatorCommunicationUri
$packet.fallbackPosture.catalogSourceMode = $CatalogSourceMode
$packet.fallbackPosture.productionLike = $productionLike
$packet.fallbackPosture.fallbackUsed = $fallbackUsed
$packet.fallbackPosture.fallbackAuthorized = [bool]$FallbackAuthorized
$packet.fallbackPosture.fallbackAuthorizationUri = $FallbackAuthorizationUri
$packet.rollbackPosture.rollbackMethod = $RollbackPlanUri
$packet.validation.postCutoverChecksDefined = -not [string]::IsNullOrWhiteSpace($OperatorCommunicationUri)

$certificationManifest = [ordered]@{
    schemaVersion = 1
    releaseId = $ReleaseId
    environment = $Environment
    generatedAtUtc = $now
    requiredEvidence = Read-EvidenceRefs -Path $EvidenceRefsPath
}

$fallbackPosture = [ordered]@{
    schemaVersion = 1
    releaseId = $ReleaseId
    environment = $Environment
    generatedAtUtc = $now
    catalogSourceMode = $CatalogSourceMode
    productionLike = $productionLike
    fallbackUsed = $fallbackUsed
    fallbackAuthorized = [bool]$FallbackAuthorized
    fallbackAuthorizationUri = $FallbackAuthorizationUri
    fallbackDisciplinePassed = (-not $productionLike) -or (-not $fallbackUsed) -or ([bool]$FallbackAuthorized -and -not [string]::IsNullOrWhiteSpace($FallbackAuthorizationUri))
}

$validationResults = [ordered]@{
    schemaVersion = 1
    releaseId = $ReleaseId
    environment = $Environment
    generatedAtUtc = $now
    validationResultsUri = $ValidationResultsUri
    bundleGenerated = $true
    inventoryHashCaptured = -not [string]::IsNullOrWhiteSpace($inventoryHash)
    fallbackPostureCaptured = $true
    evidenceRefsPath = $EvidenceRefsPath
}

$packetPath = Join-Path $bundlePath "db-major-readiness-evidence-packet.json"
$certificationPath = Join-Path $bundlePath "certification-evidence-manifest.json"
$fallbackPath = Join-Path $bundlePath "fallback-posture.json"
$validationPath = Join-Path $bundlePath "validation-results.json"

$packet | ConvertTo-Json -Depth 12 | Set-Content -Path $packetPath -Encoding utf8
$certificationManifest | ConvertTo-Json -Depth 12 | Set-Content -Path $certificationPath -Encoding utf8
$fallbackPosture | ConvertTo-Json -Depth 12 | Set-Content -Path $fallbackPath -Encoding utf8
$validationResults | ConvertTo-Json -Depth 12 | Set-Content -Path $validationPath -Encoding utf8

[ordered]@{
    bundlePath = $bundlePath
    packetPath = $packetPath
    certificationManifestPath = $certificationPath
    fallbackPosturePath = $fallbackPath
    validationResultsPath = $validationPath
} | ConvertTo-Json -Depth 4
