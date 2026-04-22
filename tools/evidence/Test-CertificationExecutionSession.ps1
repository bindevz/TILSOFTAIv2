param(
    [Parameter(Mandatory = $true)]
    [string]$SessionPath,

    [switch]$AllowBlocked,
    [switch]$AllowExampleEvidence,
    [switch]$AllowStaleEvidence
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
$allowedStatuses = @("missing", "completed", "waived", "example", "blocked_example")
$allowedStates = @("draft", "in_progress", "completed", "blocked", "expired")

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

if (-not (Test-Path $SessionPath)) {
    throw "Missing certification execution session: $SessionPath"
}

$session = Get-Content $SessionPath -Raw | ConvertFrom-Json
$errors = New-Object System.Collections.Generic.List[string]
$now = [DateTimeOffset]::UtcNow

if ($session.schemaVersion -ne 1) {
    $errors.Add("Certification execution session schemaVersion must be 1.")
}

if ($session.artifactType -ne "live-certification-execution-session") {
    $errors.Add("Certification execution session artifactType must be live-certification-execution-session.")
}

foreach ($field in @("sessionId", "releaseId", "certificationRunId", "environment", "generatedAtUtc", "sessionStartedAtUtc", "executionState")) {
    if ([string]::IsNullOrWhiteSpace([string]$session.$field)) {
        $errors.Add("Certification execution session field '$field' is required.")
    }
}

if ($allowedStates -notcontains [string]$session.executionState) {
    $errors.Add("Certification execution state '$($session.executionState)' is not supported.")
}

if (-not $AllowBlocked -and $session.executionState -ne "completed") {
    $errors.Add("Certification execution session is not completed.")
}

if (-not (Test-JsonProperty -InputObject $session -Name "executionContext")) {
    $errors.Add("Certification execution session must include executionContext.")
}
else {
    foreach ($field in @("changeTicketId", "tenantScopeRef", "executionWindowId", "operatorId", "approverId", "incidentRefs")) {
        if (-not (Test-JsonProperty -InputObject $session.executionContext -Name $field)) {
            $errors.Add("Certification executionContext field '$field' is required.")
        }
    }
}

if (-not (Test-JsonProperty -InputObject $session -Name "fallbackPosture")) {
    $errors.Add("Certification execution session must include fallbackPosture.")
}

if (-not (Test-JsonProperty -InputObject $session -Name "decisionInputs")) {
    $errors.Add("Certification execution session must include decisionInputs.")
}

if (-not (Test-JsonProperty -InputObject $session -Name "drillLedger")) {
    $errors.Add("Certification execution session must include drillLedger.")
}
else {
    $actualDrills = @($session.drillLedger | ForEach-Object { [string]$_.drillKind })
    foreach ($requiredDrill in $requiredDrills) {
        if ($actualDrills -notcontains $requiredDrill) {
            $errors.Add("Missing required drill '$requiredDrill' in drillLedger.")
        }
    }

    $missingRequired = New-Object System.Collections.Generic.List[string]
    $staleRequired = New-Object System.Collections.Generic.List[string]
    $examplePresent = $false
    $signoffComplete = $false
    foreach ($entry in $session.drillLedger) {
        $drillKind = [string]$entry.drillKind
        $status = [string]$entry.status
        $required = [bool]$entry.required
        $evidenceUri = [string]$entry.evidenceUri
        $collectedAtUtc = [string]$entry.collectedAtUtc
        $freshnessWindowDays = [int]$entry.freshnessWindowDays

        if ($allowedStatuses -notcontains $status) {
            $errors.Add("Drill '$drillKind' has unsupported status '$status'.")
        }

        if ($freshnessWindowDays -le 0) {
            $errors.Add("Drill '$drillKind' must have positive freshnessWindowDays.")
        }

        if ($status -in @("completed", "waived", "example", "blocked_example") -and [string]::IsNullOrWhiteSpace($evidenceUri)) {
            $errors.Add("Drill '$drillKind' with status '$status' requires evidenceUri.")
        }

        if (-not [string]::IsNullOrWhiteSpace($evidenceUri) -and -not (Test-EvidenceUri -Uri $evidenceUri)) {
            $errors.Add("Drill '$drillKind' has unsupported evidence URI or identifier '$evidenceUri'.")
        }

        if ($status -eq "waived" -and [string]::IsNullOrWhiteSpace([string]$entry.waiverRef)) {
            $errors.Add("Drill '$drillKind' with waived status requires waiverRef.")
        }

        if ($drillKind -eq "operator_signoff" -and $status -in @("completed", "waived")) {
            $signoffComplete = $true
        }
        elseif ($drillKind -eq "operator_signoff") {
            if ($session.executionState -eq "completed" -or -not $AllowBlocked) {
                $errors.Add("Operator signoff drill must be completed or waived.")
            }
        }

        if ($status -in @("example", "blocked_example") -or $evidenceUri -match "example") {
            $examplePresent = $true
            if (-not $AllowExampleEvidence) {
                $errors.Add("Drill '$drillKind' uses example evidence and cannot satisfy live execution.")
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($collectedAtUtc)) {
            $collectedAt = [DateTimeOffset]::MinValue
            if (-not [DateTimeOffset]::TryParse($collectedAtUtc, [ref]$collectedAt)) {
                $errors.Add("Drill '$drillKind' has invalid collectedAtUtc '$collectedAtUtc'.")
            }
            elseif ($collectedAt.AddDays($freshnessWindowDays) -lt $now) {
                $staleRequired.Add($drillKind)
                if (-not $AllowStaleEvidence) {
                    $errors.Add("Drill '$drillKind' evidence is stale.")
                }
            }
        }

        if ($required -and $status -notin @("completed", "waived")) {
            $missingRequired.Add($drillKind)
        }
    }

    if ($session.executionState -eq "completed" -and $missingRequired.Count -gt 0) {
        $errors.Add("Execution session cannot be completed with incomplete required drills: $($missingRequired -join ', ').")
    }

    if ($session.executionState -eq "completed" -and $staleRequired.Count -gt 0 -and -not $AllowStaleEvidence) {
        $errors.Add("Execution session cannot be completed with stale required drills: $($staleRequired -join ', ').")
    }

    $expectedRequiredDrillsPresent = ($requiredDrills | Where-Object { $actualDrills -contains $_ }).Count -eq $requiredDrills.Count
    if ([bool]$session.decisionInputs.requiredDrillsPresent -ne $expectedRequiredDrillsPresent) {
        $errors.Add("decisionInputs.requiredDrillsPresent does not match drill ledger coverage.")
    }

    if ([bool]$session.decisionInputs.requiredDrillsCompleted -ne ($missingRequired.Count -eq 0)) {
        $errors.Add("decisionInputs.requiredDrillsCompleted does not match drill completion state.")
    }

    if ([bool]$session.decisionInputs.exampleEvidencePresent -ne $examplePresent) {
        $errors.Add("decisionInputs.exampleEvidencePresent does not match drill ledger evidence state.")
    }

    if ([bool]$session.decisionInputs.operatorSignoffCompleted -ne $signoffComplete) {
        $errors.Add("decisionInputs.operatorSignoffCompleted does not match operator_signoff drill state.")
    }
}

$fallback = $session.fallbackPosture
if ($fallback.productionLike -and $fallback.fallbackUsed -and $fallback.fallbackDecision -ne "authorized_exception") {
    $errors.Add("Production-like fallback requires authorized_exception in execution session.")
}

if ($fallback.productionLike -and $fallback.fallbackUsed -and [string]::IsNullOrWhiteSpace([string]$fallback.fallbackAuthorizationUri)) {
    $errors.Add("Production-like fallback requires fallbackAuthorizationUri in execution session.")
}

if ($session.executionState -eq "completed" -and [string]::IsNullOrWhiteSpace([string]$session.sessionCompletedAtUtc)) {
    $errors.Add("Completed execution session requires sessionCompletedAtUtc.")
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "Certification execution session validated: $SessionPath"
