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
$allowedReviewStates = @("draft", "blocked", "dry-run", "ready_for_review")
$allowedEvidenceStatuses = @("missing", "referenced", "example", "blocked_example")
$allowedFallbackDecisions = @("unknown", "accepted", "authorized_exception", "blocked")

function Test-JsonProperty {
    param(
        [object]$InputObject,
        [string]$Name
    )

    return $null -ne $InputObject -and $InputObject.PSObject.Properties.Name -contains $Name
}

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

if ($manifest.manifestType -ne "staging-prodlike-certification-run") {
    $errors.Add("Certification manifest manifestType must be staging-prodlike-certification-run.")
}

foreach ($field in @("releaseId", "certificationRunId", "environment", "generatedAtUtc", "reviewState")) {
    if ([string]::IsNullOrWhiteSpace([string]$manifest.$field)) {
        $errors.Add("Certification manifest field '$field' is required.")
    }
}

if ($allowedReviewStates -notcontains [string]$manifest.reviewState) {
    $errors.Add("Certification manifest reviewState '$($manifest.reviewState)' is not supported.")
}

if (-not (Test-JsonProperty -InputObject $manifest -Name "executionContext")) {
    $errors.Add("Certification manifest must include executionContext metadata placeholders.")
}
else {
    foreach ($field in @("changeTicketId", "tenantScopeRef", "executionWindowId", "operatorId", "approverId", "incidentRefs")) {
        if (-not (Test-JsonProperty -InputObject $manifest.executionContext -Name $field)) {
            $errors.Add("Certification executionContext field '$field' is required.")
        }
    }
}

if (-not (Test-JsonProperty -InputObject $manifest -Name "reviewGate")) {
    $errors.Add("Certification manifest must include reviewGate decision metadata.")
}
else {
    foreach ($field in @("decision", "acceptedForReleaseReview", "decidedAtUtc", "blockers", "decisionInputs")) {
        if (-not (Test-JsonProperty -InputObject $manifest.reviewGate -Name $field)) {
            $errors.Add("Certification reviewGate field '$field' is required.")
        }
    }

    if ($allowedReviewStates -notcontains [string]$manifest.reviewGate.decision) {
        $errors.Add("Certification reviewGate decision '$($manifest.reviewGate.decision)' is not supported.")
    }

    if ([string]$manifest.reviewGate.decision -ne [string]$manifest.reviewState) {
        $errors.Add("Certification reviewGate decision must match reviewState.")
    }
}

if ($allowedFallbackDecisions -notcontains [string]$manifest.fallbackPosture.fallbackDecision) {
    $errors.Add("Certification fallbackDecision '$($manifest.fallbackPosture.fallbackDecision)' is not supported.")
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

    if ($allowedEvidenceStatuses -notcontains $status) {
        $errors.Add("Evidence '$kind' has unsupported status '$status'.")
    }

    if ($item.required -and [string]::IsNullOrWhiteSpace($uri) -and -not $AllowMissingEvidence) {
        $errors.Add("Required evidence '$kind' is missing.")
    }

    if (-not [string]::IsNullOrWhiteSpace($uri) -and -not (Test-EvidenceUri -Uri $uri)) {
        $errors.Add("Evidence '$kind' has an unsupported URI or identifier: $uri")
    }

    if (($status -eq "example" -or $status -eq "blocked_example" -or $uri -match "example") -and -not $AllowExampleEvidence) {
        $errors.Add("Evidence '$kind' uses example evidence and cannot satisfy live certification.")
    }

    if (-not [string]::IsNullOrWhiteSpace($uri)) {
        if ([string]::IsNullOrWhiteSpace([string]$item.collectedAtUtc)) {
            $errors.Add("Evidence '$kind' must include collectedAtUtc when evidenceUri is present.")
        }
        else {
            $parsed = [DateTimeOffset]::MinValue
            if (-not [DateTimeOffset]::TryParse([string]$item.collectedAtUtc, [ref]$parsed)) {
                $errors.Add("Evidence '$kind' has invalid collectedAtUtc '$($item.collectedAtUtc)'.")
            }
        }
    }

    if ([int]$item.freshnessWindowDays -le 0) {
        $errors.Add("Evidence '$kind' must have a positive freshnessWindowDays value.")
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

if (Test-JsonProperty -InputObject $manifest -Name "reviewGate") {
    $expectedAccepted = $manifest.reviewState -eq "ready_for_review"
    if ([bool]$manifest.reviewGate.acceptedForReleaseReview -ne $expectedAccepted) {
        $errors.Add("Certification reviewGate acceptedForReleaseReview must match ready_for_review state.")
    }
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "Certification run manifest validated: $ManifestPath"
