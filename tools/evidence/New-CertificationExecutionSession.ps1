param(
    [Parameter(Mandatory = $true)]
    [string]$CertificationRunPath,

    [string]$OutputPath = "",
    [string]$SessionId = "",
    [string]$SessionStartedAtUtc = "",
    [string]$SessionCompletedAtUtc = "",
    [string]$TrustedEvidencePath = "",
    [string]$TrustPolicyPath = "",
    [string]$WaiverPath = "",
    [string[]]$WaivedDrillKinds = @(),
    [string]$WaiverNote = "",
    [string]$WaiverAuthority = "release_authority"
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

function Get-StringProperty {
    param(
        [object]$InputObject,
        [string]$Name,
        [string]$DefaultValue = ""
    )

    if ($null -eq $InputObject) {
        return $DefaultValue
    }

    if ($InputObject.PSObject.Properties.Name -contains $Name) {
        $value = [string]$InputObject.$Name
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    return $DefaultValue
}

function Get-BoolProperty {
    param(
        [object]$InputObject,
        [string]$Name,
        [bool]$DefaultValue = $false
    )

    if ($null -eq $InputObject) {
        return $DefaultValue
    }

    if ($InputObject.PSObject.Properties.Name -contains $Name) {
        return [bool]$InputObject.$Name
    }

    return $DefaultValue
}

function Get-StringArrayProperty {
    param(
        [object]$InputObject,
        [string]$Name
    )

    if ($null -eq $InputObject -or -not ($InputObject.PSObject.Properties.Name -contains $Name)) {
        return @()
    }

    return @($InputObject.$Name | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-TrustTierRank {
    param([string]$TrustTier)

    switch -Regex ($TrustTier.ToLowerInvariant()) {
        "^signature_verified$" { return 3 }
        "^provider_verified$" { return 2 }
        default { return 1 }
    }
}

function Resolve-EnvironmentPolicyName {
    param([string]$EnvironmentName)

    $value = if ($null -eq $EnvironmentName) { "" } else { $EnvironmentName.Trim().ToLowerInvariant() }
    switch ($value) {
        "prod" { return "production" }
        "production" { return "production" }
        "prod-like" { return "prod-like" }
        "prodlike" { return "prod-like" }
        default { return $value }
    }
}

function Get-EffectiveTrustPolicy {
    param(
        [object]$Policy,
        [string]$EnvironmentName
    )

    $defaults = if ($null -ne $Policy -and $Policy.PSObject.Properties.Name -contains "defaults") { $Policy.defaults } else { $null }
    $envName = Resolve-EnvironmentPolicyName -EnvironmentName $EnvironmentName
    $envPolicy = $null
    if ($null -ne $Policy -and $Policy.PSObject.Properties.Name -contains "environments") {
        foreach ($property in $Policy.environments.PSObject.Properties) {
            if ([string]::Equals($property.Name, $envName, [System.StringComparison]::OrdinalIgnoreCase)) {
                $envPolicy = $property.Value
                break
            }
        }
    }

    $minimumTrustTier = Get-StringProperty -InputObject $envPolicy -Name "minimumTrustTier" -DefaultValue (Get-StringProperty -InputObject $defaults -Name "minimumTrustTier" -DefaultValue "metadata_verified")
    $acceptedVerifierClasses = Get-StringArrayProperty -InputObject $envPolicy -Name "acceptedVerifierClasses"
    if ($acceptedVerifierClasses.Count -eq 0) {
        $acceptedVerifierClasses = Get-StringArrayProperty -InputObject $defaults -Name "acceptedVerifierClasses"
    }

    $allowedUriPrefixes = Get-StringArrayProperty -InputObject $envPolicy -Name "allowedUriPrefixes"
    if ($allowedUriPrefixes.Count -eq 0) {
        $allowedUriPrefixes = Get-StringArrayProperty -InputObject $defaults -Name "allowedUriPrefixes"
    }

    $requireProviderProof = Get-StringArrayProperty -InputObject $envPolicy -Name "requireProviderProvenanceProofForVerifierClasses"
    if ($requireProviderProof.Count -eq 0) {
        $requireProviderProof = Get-StringArrayProperty -InputObject $defaults -Name "requireProviderProvenanceProofForVerifierClasses"
    }

    return [ordered]@{
        policyVersion = Get-StringProperty -InputObject $Policy -Name "policyVersion" -DefaultValue "sprint-30"
        policyEnvironment = if ([string]::IsNullOrWhiteSpace($envName)) { "default" } else { $envName }
        minimumTrustTier = $minimumTrustTier
        acceptedVerifierClasses = @($acceptedVerifierClasses)
        allowedUriPrefixes = @($allowedUriPrefixes)
        requireProviderProvenanceProofForVerifierClasses = @($requireProviderProof)
    }
}

function Test-Sha256Hex {
    param([string]$Value)
    return -not [string]::IsNullOrWhiteSpace($Value) -and $Value -match "^[0-9a-fA-F]{64}$"
}

function Normalize-WaiverEntries {
    param(
        [object]$WaiverDocument,
        [DateTimeOffset]$UtcNow
    )

    $entries = @()
    if ($null -eq $WaiverDocument) {
        return $entries
    }

    foreach ($waiver in @($WaiverDocument)) {
        $linkedDrills = @(Get-StringArrayProperty -InputObject $waiver -Name "linkedDrills")
        $waiverId = Get-StringProperty -InputObject $waiver -Name "waiverId"
        if ([string]::IsNullOrWhiteSpace($waiverId)) {
            $waiverId = "waiver://generated/$([guid]::NewGuid().ToString('N'))"
        }

        $scope = Get-StringProperty -InputObject $waiver -Name "scope" -DefaultValue "drill"
        $authority = Get-StringProperty -InputObject $waiver -Name "authority"
        $reason = Get-StringProperty -InputObject $waiver -Name "reason"
        $expiresAtUtc = Get-StringProperty -InputObject $waiver -Name "expiresAtUtc"
        $nonWaivable = Get-BoolProperty -InputObject $waiver -Name "nonWaivable" -DefaultValue $false
        $status = "active"

        if ([string]::IsNullOrWhiteSpace($authority) -or [string]::IsNullOrWhiteSpace($reason) -or $linkedDrills.Count -eq 0) {
            $status = "invalid"
        }
        elseif (-not [string]::IsNullOrWhiteSpace($expiresAtUtc)) {
            $expiresAt = [DateTimeOffset]::MinValue
            if (-not [DateTimeOffset]::TryParse($expiresAtUtc, [ref]$expiresAt)) {
                $status = "invalid"
            }
            elseif ($expiresAt -le $UtcNow) {
                $status = "expired"
            }
        }

        $entries += [ordered]@{
            waiverId = $waiverId
            authority = $authority
            scope = $scope
            reason = $reason
            expiresAtUtc = $expiresAtUtc
            linkedDrills = @($linkedDrills)
            nonWaivable = $nonWaivable
            status = $status
        }
    }

    return $entries
}

$repoRoot = Resolve-RepoRoot
$templatePath = Join-Path $repoRoot "docs/certification_execution_session.template.json"
if (-not (Test-Path $templatePath)) {
    throw "Missing certification execution session template."
}

$resolvedCertificationRunPath = Resolve-ArtifactPath -Path $CertificationRunPath -RepoRoot $repoRoot
$certification = Read-JsonFile -Path $resolvedCertificationRunPath

if ([string]::IsNullOrWhiteSpace($TrustPolicyPath)) {
    $TrustPolicyPath = Join-Path $repoRoot "docs/certification_execution_trust_policy.template.json"
}
elseif (-not [System.IO.Path]::IsPathRooted($TrustPolicyPath)) {
    $TrustPolicyPath = Join-Path $repoRoot $TrustPolicyPath
}

$trustPolicyDocument = Read-JsonFile -Path $TrustPolicyPath
$effectiveTrustPolicy = Get-EffectiveTrustPolicy -Policy $trustPolicyDocument -EnvironmentName ([string]$certification.environment
)

if (-not [string]::IsNullOrWhiteSpace($TrustedEvidencePath) -and -not [System.IO.Path]::IsPathRooted($TrustedEvidencePath)) {
    $TrustedEvidencePath = Join-Path $repoRoot $TrustedEvidencePath
}

$trustedEvidenceByKind = @{}
if (-not [string]::IsNullOrWhiteSpace($TrustedEvidencePath)) {
    $trustedEvidenceDocument = Read-JsonFile -Path $TrustedEvidencePath
    foreach ($property in $trustedEvidenceDocument.PSObject.Properties) {
        $trustedEvidenceByKind[[string]$property.Name] = $property.Value
    }
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path -Parent $resolvedCertificationRunPath) "certification-execution-session.json"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repoRoot $OutputPath
}

$now = [DateTimeOffset]::UtcNow
if ([string]::IsNullOrWhiteSpace($SessionStartedAtUtc)) {
    $SessionStartedAtUtc = if ([string]::IsNullOrWhiteSpace([string]$certification.generatedAtUtc)) { $now.ToString("o") } else { [string]$certification.generatedAtUtc }
}

if ([string]::IsNullOrWhiteSpace($SessionId)) {
    $SessionId = "$($certification.releaseId)-$($certification.environment)-execution"
}

$session = Get-Content $templatePath -Raw | ConvertFrom-Json
$waivedLookup = @{}
$nonWaivableDrills = @("operator_signoff")
$waiverEntries = @()

if (-not [string]::IsNullOrWhiteSpace($WaiverPath)) {
    if (-not [System.IO.Path]::IsPathRooted($WaiverPath)) {
        $WaiverPath = Join-Path $repoRoot $WaiverPath
    }

    $waiverEntries += Normalize-WaiverEntries -WaiverDocument (Read-JsonFile -Path $WaiverPath) -UtcNow $now
}

foreach ($drill in $WaivedDrillKinds) {
    if ([string]::IsNullOrWhiteSpace($drill)) {
        continue
    }

    $waiverEntries += [ordered]@{
        waiverId = "waiver://$drill"
        authority = $WaiverAuthority
        scope = "drill"
        reason = if ([string]::IsNullOrWhiteSpace($WaiverNote)) { "Waived by release authority." } else { $WaiverNote }
        expiresAtUtc = $now.AddDays(7).ToString("o")
        linkedDrills = @($drill)
        nonWaivable = $false
        status = "active"
    }
}

foreach ($waiver in $waiverEntries) {
    if ([string]$waiver.status -ne "active") {
        continue
    }

    foreach ($drill in @($waiver.linkedDrills)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$drill) -and -not $waivedLookup.ContainsKey($drill)) {
            $waivedLookup[$drill] = [string]$waiver.waiverId
        }
    }
}

$sourceEvidence = @{}
foreach ($item in $certification.requiredEvidence) {
    $sourceEvidence[[string]$item.evidenceKind] = $item
}

$staleDrills = New-Object System.Collections.Generic.List[string]
$missingRequired = New-Object System.Collections.Generic.List[string]
$exampleDrills = New-Object System.Collections.Generic.List[string]
$trustUnboundDrills = New-Object System.Collections.Generic.List[string]
$trustUnverifiedDrills = New-Object System.Collections.Generic.List[string]
$trustPolicyFailedDrills = New-Object System.Collections.Generic.List[string]
$providerProofGapDrills = New-Object System.Collections.Generic.List[string]
$invalidWaiverPresent = $false
$nonWaivableViolationPresent = $false

foreach ($entry in $session.drillLedger) {
    $drillKind = [string]$entry.drillKind
    $source = if ($sourceEvidence.ContainsKey($drillKind)) { $sourceEvidence[$drillKind] } else { $null }
    $trustedSource = if ($trustedEvidenceByKind.ContainsKey($drillKind)) { $trustedEvidenceByKind[$drillKind] } else { $null }
    $evidenceUri = if ($null -eq $source) { "" } else { [string]$source.evidenceUri }
    $sourceStatus = if ($null -eq $source) { "missing" } else { [string]$source.status }
    $collectedAt = if ($null -eq $source) { "" } else { [string]$source.collectedAtUtc }
    $freshness = if ($null -eq $source) { [int]$entry.freshnessWindowDays } else { [int]$source.freshnessWindowDays }
    if ($freshness -le 0) {
        $freshness = if ($drillKind -eq "operator_signoff") { 14 } else { 30 }
    }

    $entry.evidenceUri = $evidenceUri
    $entry.evidenceRecordId = Get-StringProperty -InputObject $trustedSource -Name "evidenceRecordId"
    $entry.artifactHash = (Get-StringProperty -InputObject $trustedSource -Name "artifactHash").ToLowerInvariant()
    $entry.artifactHashAlgorithm = Get-StringProperty -InputObject $trustedSource -Name "artifactHashAlgorithm" -DefaultValue "sha256"
    $entry.verificationStatus = (Get-StringProperty -InputObject $trustedSource -Name "verificationStatus" -DefaultValue "unverified").ToLowerInvariant()
    $entry.verifierClass = (Get-StringProperty -InputObject $trustedSource -Name "verifierClass" -DefaultValue "metadata").ToLowerInvariant()
    $entry.trustTier = (Get-StringProperty -InputObject $trustedSource -Name "trustTier" -DefaultValue "metadata_verified").ToLowerInvariant()
    $entry.sourceSystem = Get-StringProperty -InputObject $trustedSource -Name "sourceSystem"
    $entry.providerId = Get-StringProperty -InputObject $trustedSource -Name "providerId"
    $entry.providerProvenanceProofUri = Get-StringProperty -InputObject $trustedSource -Name "providerProvenanceProofUri"
    $entry.providerHashRecomputed = Get-BoolProperty -InputObject $trustedSource -Name "providerHashRecomputed" -DefaultValue $false
    $entry.policyMinimumTrustTier = [string]$effectiveTrustPolicy.minimumTrustTier
    $entry.policyAcceptedVerifierClasses = @($effectiveTrustPolicy.acceptedVerifierClasses)
    $entry.executedBy = if ([string]::IsNullOrWhiteSpace([string]$certification.executionContext.operatorId)) { "" } else { [string]$certification.executionContext.operatorId }
    $entry.startedAtUtc = if ([string]::IsNullOrWhiteSpace($collectedAt)) { "" } else { $collectedAt }
    $entry.completedAtUtc = if ([string]::IsNullOrWhiteSpace($collectedAt)) { "" } else { $collectedAt }
    $entry.collectedAtUtc = $collectedAt
    $entry.freshnessWindowDays = $freshness
    $entry.note = ""
    $entry.waiverRef = ""
    $entry.allowedUriPrefixMatched = [bool](@($effectiveTrustPolicy.allowedUriPrefixes).Count -eq 0 -or
        (-not [string]::IsNullOrWhiteSpace($evidenceUri) -and @($effectiveTrustPolicy.allowedUriPrefixes | Where-Object { $evidenceUri.StartsWith($_, [System.StringComparison]::OrdinalIgnoreCase) }).Count -gt 0))
    $entry.trustDecision = "blocked"

    if ($waivedLookup.ContainsKey($drillKind)) {
        $entry.status = "waived"
        $entry.note = if ([string]::IsNullOrWhiteSpace($WaiverNote)) { "Waived by release authority." } else { $WaiverNote }
        $entry.waiverRef = [string]$waivedLookup[$drillKind]
        if ($nonWaivableDrills -contains $drillKind) {
            $entry.trustDecision = "blocked"
            $nonWaivableViolationPresent = $true
        }
        else {
            $entry.trustDecision = "waived"
        }
    }
    elseif ([string]::IsNullOrWhiteSpace($evidenceUri)) {
        $entry.status = "missing"
        if ([bool]$entry.required) {
            $missingRequired.Add($drillKind)
        }
    }
    elseif ($sourceStatus -in @("example", "blocked_example") -or $evidenceUri -match "example") {
        $entry.status = "example"
        $exampleDrills.Add($drillKind)
    }
    else {
        $entry.status = "completed"
    }

    $providerProofRequired = @($effectiveTrustPolicy.requireProviderProvenanceProofForVerifierClasses | Where-Object { [string]::Equals($_, [string]$entry.verifierClass, [System.StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
    $providerProofSatisfied = -not $providerProofRequired -or [bool]$entry.providerHashRecomputed -or -not [string]::IsNullOrWhiteSpace([string]$entry.providerProvenanceProofUri)
    $hashPresent = Test-Sha256Hex -Value ([string]$entry.artifactHash)
    $verified = [string]$entry.verificationStatus -in @("verified", "accepted")
    $trustTierSatisfied = Get-TrustTierRank -TrustTier ([string]$entry.trustTier) -ge (Get-TrustTierRank -TrustTier ([string]$effectiveTrustPolicy.minimumTrustTier))
    $verifierClassAccepted = @($effectiveTrustPolicy.acceptedVerifierClasses).Count -eq 0 -or
        @($effectiveTrustPolicy.acceptedVerifierClasses | Where-Object { [string]::Equals($_, [string]$entry.verifierClass, [System.StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
    $boundToRecord = -not [string]::IsNullOrWhiteSpace([string]$entry.evidenceRecordId)

    if ($entry.status -eq "completed") {
        if (-not $boundToRecord) {
            $trustUnboundDrills.Add($drillKind)
        }

        if (-not $hashPresent -or -not $verified) {
            $trustUnverifiedDrills.Add($drillKind)
        }

        if (-not $entry.allowedUriPrefixMatched -or -not $trustTierSatisfied -or -not $verifierClassAccepted) {
            $trustPolicyFailedDrills.Add($drillKind)
        }

        if (-not $providerProofSatisfied) {
            $providerProofGapDrills.Add($drillKind)
        }

        if ($boundToRecord -and $hashPresent -and $verified -and $entry.allowedUriPrefixMatched -and $trustTierSatisfied -and $verifierClassAccepted -and $providerProofSatisfied) {
            $entry.trustDecision = "accepted"
        }
    }

    if ($entry.status -in @("completed", "waived") -and -not [string]::IsNullOrWhiteSpace($entry.collectedAtUtc)) {
        $collected = [DateTimeOffset]::Parse([string]$entry.collectedAtUtc)
        if ($collected.AddDays([int]$entry.freshnessWindowDays) -lt $now) {
            $staleDrills.Add($drillKind)
        }
    }
}

$requiredPresent = @($session.drillLedger | Where-Object { $_.required }).Count -eq $requiredDrills.Count
$requiredCompleted = @($session.drillLedger | Where-Object { $_.required -and $_.status -notin @("completed", "waived") }).Count -eq 0
$examplePresent = $exampleDrills.Count -gt 0
$stalePresent = $staleDrills.Count -gt 0
$fallback = $certification.fallbackPosture
$fallbackAccepted = -not ($fallback.productionLike -and $fallback.fallbackUsed -and $fallback.fallbackDecision -ne "authorized_exception")
$signoffEntry = $session.drillLedger | Where-Object { $_.drillKind -eq "operator_signoff" } | Select-Object -First 1
$signoffCompleted = $null -ne $signoffEntry -and $signoffEntry.status -in @("completed", "waived")
$trustedEvidenceBound = @($session.drillLedger | Where-Object { $_.required -and $_.status -eq "completed" -and [string]::IsNullOrWhiteSpace([string]$_.evidenceRecordId) }).Count -eq 0
$trustedEvidenceVerified = @($session.drillLedger | Where-Object { $_.required -and $_.status -eq "completed" -and ((-not (Test-Sha256Hex -Value ([string]$_.artifactHash))) -or [string]$_.verificationStatus -notin @("verified", "accepted")) }).Count -eq 0
$trustedEvidencePolicySatisfied = @($session.drillLedger | Where-Object { $_.required -and $_.status -eq "completed" -and [string]$_.trustDecision -ne "accepted" }).Count -eq 0
$providerProvenanceComplete = $providerProofGapDrills.Count -eq 0

foreach ($waiver in $waiverEntries) {
    if ([string]$waiver.status -ne "active") {
        $invalidWaiverPresent = $true
    }

    foreach ($drill in @($waiver.linkedDrills)) {
        if ($nonWaivableDrills -contains [string]$drill) {
            $nonWaivableViolationPresent = $true
        }
    }
}

$trustedEvidenceReady = $trustedEvidenceBound -and $trustedEvidenceVerified -and $trustedEvidencePolicySatisfied -and $providerProvenanceComplete -and -not $invalidWaiverPresent -and -not $nonWaivableViolationPresent

$blockers = @()
if (-not $requiredPresent) {
    $blockers += "required_drill_set_incomplete"
}
if ($missingRequired.Count -gt 0) {
    $blockers += "missing_required_drill"
}
if ($examplePresent) {
    $blockers += "example_evidence"
}
if ($stalePresent) {
    $blockers += "stale_evidence"
}
if (-not $fallbackAccepted) {
    $blockers += "fallback_authorization_gap"
}
if (-not $signoffCompleted) {
    $blockers += "operator_signoff_incomplete"
}
if (-not $trustedEvidenceBound) {
    $blockers += "trusted_evidence_unbound"
}
if (-not $trustedEvidenceVerified) {
    $blockers += "trusted_evidence_unverified"
}
if (-not $trustedEvidencePolicySatisfied) {
    $blockers += "trusted_evidence_policy_violation"
}
if (-not $providerProvenanceComplete) {
    $blockers += "provider_provenance_gap"
}
if ($invalidWaiverPresent) {
    $blockers += "invalid_waiver"
}
if ($nonWaivableViolationPresent) {
    $blockers += "nonwaivable_waiver_violation"
}

$session.releaseId = $certification.releaseId
$session.certificationRunId = $certification.certificationRunId
$session.environment = $certification.environment
$session.sessionId = $SessionId
$session.generatedAtUtc = $now.ToString("o")
$session.sessionStartedAtUtc = $SessionStartedAtUtc
$session.sessionCompletedAtUtc = if ($blockers.Count -eq 0) {
    if ([string]::IsNullOrWhiteSpace($SessionCompletedAtUtc)) { $now.ToString("o") } else { $SessionCompletedAtUtc }
}
else {
    ""
}
$session.executionState = if ($blockers.Count -eq 0) { "completed" } else { "blocked" }
$session.executionContext.changeTicketId = $certification.executionContext.changeTicketId
$session.executionContext.tenantScopeRef = $certification.executionContext.tenantScopeRef
$session.executionContext.executionWindowId = $certification.executionContext.executionWindowId
$session.executionContext.operatorId = $certification.executionContext.operatorId
$session.executionContext.approverId = $certification.executionContext.approverId
$session.executionContext.incidentRefs = @($certification.executionContext.incidentRefs)
$session.fallbackPosture.catalogSourceMode = $fallback.catalogSourceMode
$session.fallbackPosture.productionLike = [bool]$fallback.productionLike
$session.fallbackPosture.fallbackUsed = [bool]$fallback.fallbackUsed
$session.fallbackPosture.fallbackAuthorized = [bool]$fallback.fallbackAuthorized
$session.fallbackPosture.fallbackAuthorizationUri = [string]$fallback.fallbackAuthorizationUri
$session.fallbackPosture.fallbackDecision = [string]$fallback.fallbackDecision
$session.decisionInputs.requiredDrillsPresent = $requiredPresent
$session.decisionInputs.requiredDrillsCompleted = $requiredCompleted
$session.decisionInputs.exampleEvidencePresent = $examplePresent
$session.decisionInputs.staleEvidencePresent = $stalePresent
$session.decisionInputs.fallbackAccepted = $fallbackAccepted
$session.decisionInputs.operatorSignoffCompleted = $signoffCompleted
$session.decisionInputs.trustedEvidenceBound = $trustedEvidenceBound
$session.decisionInputs.trustedEvidenceVerified = $trustedEvidenceVerified
$session.decisionInputs.trustedEvidencePolicySatisfied = $trustedEvidencePolicySatisfied
$session.decisionInputs.providerProvenanceComplete = $providerProvenanceComplete
$session.decisionInputs.invalidWaiverPresent = $invalidWaiverPresent
$session.decisionInputs.nonWaivableViolationPresent = $nonWaivableViolationPresent
$session.decisionInputs.trustedEvidenceReady = $trustedEvidenceReady
$session.trustPolicy.policyVersion = [string]$effectiveTrustPolicy.policyVersion
$session.trustPolicy.policyEnvironment = [string]$effectiveTrustPolicy.policyEnvironment
$session.trustPolicy.minimumTrustTier = [string]$effectiveTrustPolicy.minimumTrustTier
$session.trustPolicy.acceptedVerifierClasses = @($effectiveTrustPolicy.acceptedVerifierClasses)
$session.trustPolicy.allowedUriPrefixes = @($effectiveTrustPolicy.allowedUriPrefixes)
$session.trustPolicy.requireProviderProvenanceProofForVerifierClasses = @($effectiveTrustPolicy.requireProviderProvenanceProofForVerifierClasses)
$session.waivers = @($waiverEntries)
$session.blockers = @($blockers | Sort-Object -Unique)

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$session | ConvertTo-Json -Depth 12 | Set-Content -Path $OutputPath -Encoding utf8

[ordered]@{
    sessionPath = $OutputPath
    sessionId = $session.sessionId
    executionState = $session.executionState
    blockers = $session.blockers
    staleDrills = @($staleDrills | Select-Object -Unique)
    trustUnboundDrills = @($trustUnboundDrills | Select-Object -Unique)
    trustUnverifiedDrills = @($trustUnverifiedDrills | Select-Object -Unique)
    trustPolicyFailedDrills = @($trustPolicyFailedDrills | Select-Object -Unique)
    providerProofGapDrills = @($providerProofGapDrills | Select-Object -Unique)
} | ConvertTo-Json -Depth 6
