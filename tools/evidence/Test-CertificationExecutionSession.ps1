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
$nonWaivableDrills = @("operator_signoff")

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

function Test-Sha256Hex {
    param([string]$Value)

    return -not [string]::IsNullOrWhiteSpace($Value) -and $Value -match "^[0-9a-fA-F]{64}$"
}

function Get-TrustTierRank {
    param([string]$TrustTier)

    $value = if ($null -eq $TrustTier) { "" } else { $TrustTier.ToLowerInvariant() }
    switch -Regex ($value) {
        "^signature_verified$" { return 3 }
        "^provider_verified$" { return 2 }
        default { return 1 }
    }
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

if (-not (Test-JsonProperty -InputObject $session -Name "trustPolicy")) {
    $errors.Add("Certification execution session must include trustPolicy.")
}
else {
    foreach ($field in @("policyVersion", "policyEnvironment", "minimumTrustTier", "acceptedVerifierClasses", "allowedUriPrefixes", "requireProviderProvenanceProofForVerifierClasses")) {
        if (-not (Test-JsonProperty -InputObject $session.trustPolicy -Name $field)) {
            $errors.Add("Certification execution trustPolicy field '$field' is required.")
        }
    }
}

if (-not (Test-JsonProperty -InputObject $session -Name "waivers")) {
    $errors.Add("Certification execution session must include waivers.")
}
else {
    foreach ($waiver in @($session.waivers)) {
        foreach ($field in @("waiverId", "authority", "scope", "reason", "expiresAtUtc", "linkedDrills", "nonWaivable", "status")) {
            if (-not (Test-JsonProperty -InputObject $waiver -Name $field)) {
                $errors.Add("Waiver entry is missing required field '$field'.")
            }
        }

        if ([string]$waiver.status -eq "active") {
            if ([string]::IsNullOrWhiteSpace([string]$waiver.authority) -or [string]::IsNullOrWhiteSpace([string]$waiver.reason)) {
                $errors.Add("Active waiver '$($waiver.waiverId)' requires authority and reason.")
            }

            if (@($waiver.linkedDrills).Count -eq 0) {
                $errors.Add("Active waiver '$($waiver.waiverId)' requires linked drills.")
            }

            $waiverExpiry = [DateTimeOffset]::MinValue
            if (-not [DateTimeOffset]::TryParse([string]$waiver.expiresAtUtc, [ref]$waiverExpiry)) {
                $errors.Add("Active waiver '$($waiver.waiverId)' has invalid expiresAtUtc.")
            }
            elseif ($waiverExpiry -le $now) {
                $errors.Add("Active waiver '$($waiver.waiverId)' is expired.")
            }

            foreach ($linkedDrill in @($waiver.linkedDrills)) {
                if ($nonWaivableDrills -contains [string]$linkedDrill) {
                    $errors.Add("Waiver '$($waiver.waiverId)' targets non-waivable drill '$linkedDrill'.")
                }
            }
        }
    }
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
        $verificationStatus = ([string]$entry.verificationStatus).ToLowerInvariant()
        $trustTier = ([string]$entry.trustTier).ToLowerInvariant()
        $verifierClass = ([string]$entry.verifierClass).ToLowerInvariant()
        $trustDecision = ([string]$entry.trustDecision).ToLowerInvariant()
        $artifactHash = [string]$entry.artifactHash

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

        foreach ($requiredField in @("evidenceRecordId", "artifactHash", "artifactHashAlgorithm", "verificationStatus", "verifierClass", "trustTier", "trustDecision", "sourceSystem", "providerId", "providerProvenanceProofUri", "providerHashRecomputed", "allowedUriPrefixMatched", "policyMinimumTrustTier", "policyAcceptedVerifierClasses", "executedBy", "startedAtUtc", "completedAtUtc")) {
            if (-not (Test-JsonProperty -InputObject $entry -Name $requiredField)) {
                $errors.Add("Drill '$drillKind' is missing trusted evidence field '$requiredField'.")
            }
        }

        if ($status -eq "completed") {
            if ([string]::IsNullOrWhiteSpace([string]$entry.evidenceRecordId)) {
                $errors.Add("Drill '$drillKind' completed state requires evidenceRecordId.")
            }

            if (-not (Test-Sha256Hex -Value $artifactHash)) {
                $errors.Add("Drill '$drillKind' completed state requires SHA-256 artifactHash.")
            }

            if ($verificationStatus -notin @("verified", "accepted")) {
                $errors.Add("Drill '$drillKind' completed state requires verified evidence.")
            }

            if ($trustDecision -ne "accepted") {
                $errors.Add("Drill '$drillKind' completed state requires accepted trustDecision.")
            }

            if (-not [bool]$entry.allowedUriPrefixMatched) {
                $errors.Add("Drill '$drillKind' completed state has disallowed evidence URI prefix.")
            }

            if (@($session.trustPolicy.acceptedVerifierClasses).Count -gt 0 -and @($session.trustPolicy.acceptedVerifierClasses | Where-Object { [string]::Equals($_, $verifierClass, [System.StringComparison]::OrdinalIgnoreCase) }).Count -eq 0) {
                $errors.Add("Drill '$drillKind' uses unaccepted verifierClass '$verifierClass'.")
            }

            if ((Get-TrustTierRank -TrustTier $trustTier) -lt (Get-TrustTierRank -TrustTier ([string]$entry.policyMinimumTrustTier))) {
                $errors.Add("Drill '$drillKind' trustTier '$trustTier' is below minimum '$([string]$entry.policyMinimumTrustTier)'.")
            }

            $providerProofRequired = @($session.trustPolicy.requireProviderProvenanceProofForVerifierClasses | Where-Object { [string]::Equals($_, $verifierClass, [System.StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
            if ($providerProofRequired -and -not [bool]$entry.providerHashRecomputed -and [string]::IsNullOrWhiteSpace([string]$entry.providerProvenanceProofUri)) {
                $errors.Add("Drill '$drillKind' requires provider provenance proof.")
            }
        }
        elseif ($status -eq "waived" -and $trustDecision -ne "waived") {
            $errors.Add("Drill '$drillKind' waived state requires trustDecision waived.")
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

    foreach ($field in @("trustedEvidenceBound", "trustedEvidenceVerified", "trustedEvidencePolicySatisfied", "providerProvenanceComplete", "invalidWaiverPresent", "nonWaivableViolationPresent", "trustedEvidenceReady")) {
        if (-not (Test-JsonProperty -InputObject $session.decisionInputs -Name $field)) {
            $errors.Add("decisionInputs.$field is required.")
        }
    }

    if (-not [bool]$session.decisionInputs.trustedEvidenceReady -and $session.executionState -eq "completed") {
        $errors.Add("Completed execution session requires trustedEvidenceReady.")
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
