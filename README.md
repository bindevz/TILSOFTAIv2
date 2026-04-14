# TILSOFTAI V3

TILSOFTAI is an enterprise-grade internal AI platform with a supervisor-native, agent-native runtime. Runtime ownership is intentionally simple:

- Supervisor orchestration owns request classification and agent dispatch.
- Domain Agents own business-domain routing and policy.
- Tool Adapters and infrastructure own execution boundaries, provider integration, persistence, and external connections.

Technical provider/model concerns are not a business-domain ownership boundary. Production capability ownership lives in the platform catalog and adapter layer, not in module packages.

## Current Runtime Shape

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
     -> IIntentClassifier
     -> CapabilityRequestHint
     -> IAgentRegistry
        -> WarehouseAgent          (business-domain routing, SQL + REST/JSON capabilities)
        -> AccountingAgent         (business-domain routing, SQL + REST/JSON capabilities)
        -> GeneralChatAgent        (native general response and retired legacy notices)

Native capability path:
  DomainAgent
    -> ICapabilityRegistry.GetByDomain(domain)
    -> ICapabilityResolver.Resolve(hint, candidates)
    -> CapabilityAccessPolicy.Evaluate(required roles, allowed tenants)
    -> CapabilityArgumentValidator.Validate(typed contract, arguments)
    -> IToolAdapterRegistry.Resolve(adapterType)
    -> IToolAdapter.ExecuteAsync(request)

Capability registry:
  CompositeCapabilityRegistry
    -> StaticCapabilitySource (WarehouseCapabilities, AccountingCapabilities)
    -> ConfigurationCapabilitySource (bootstrap only)
    -> PlatformCatalogCapabilitySource (durable platform records)

External connection catalog:
  CompositeExternalConnectionCatalog
    -> PlatformExternalConnectionCatalog (primary)
    -> ConfigurationExternalConnectionCatalog (bootstrap fallback when enabled)

Catalog control plane:
  PlatformCatalogController
    -> IPlatformCatalogControlPlane
    -> preview / submit / approve / reject / apply
    -> expected version + idempotency + risk policy
    -> IPlatformCatalogPromotionGate
    -> IPlatformCatalogCertificationStore
    -> IPlatformCatalogPromotionManifestService
    -> IPlatformCatalogPromotionManifestStore
    -> IPlatformCatalogMutationStore
    -> PlatformCatalogChangeRequest
    -> PlatformCapabilityCatalog / PlatformExternalConnectionCatalog
    -> signed evidence, promotion manifests, rollout attestations
    -> managed durable dossier archive and signer trust recovery

Write requests:
  -> IApprovalEngine (create -> approve -> execute lifecycle)
     -> IActionRequestStore
     -> IWriteActionGuard
     -> SqlToolAdapter
```

## Current Assurance Posture

- Platform catalog records are the production source of truth for capabilities and external connections.
- Catalog mutations use preview, submit, independent review, approve/reject, and apply.
- Promotion gates bind source mode, preview validity, approved change state, expected version posture, break-glass posture, and certification evidence.
- Evidence verification supports provider-controlled artifact checks, RSA signatures, signer lifecycle snapshots, trust tiers, freshness policy, and retention policy.
- Promotion manifests are immutable, rollout attestations are append-only, and audit dossiers are hash-bound.
- Dossier archives and signer trust-store recovery support managed SQL durability with explicit backend class, retention posture, immutability posture, and custody metadata.

## Remaining Bounded Residue

These components remain intentionally narrow:

- Legacy capability-scope SQL tables: compatibility-only; `ModuleKey` column names are not runtime ownership.
- `TILSOFTAI.Modules.Platform`: solution-local package residue only; the API project no longer references or loads it.
- `TILSOFTAI.Modules.Analytics`: solution-local diagnostic package residue only; the API project no longer references or loads it.
- `InMemoryCapabilityRegistry`: test fixture only.

The obsolete Model module was removed in Sprint 19. Do not reintroduce a technical model/provider module or pseudo-domain to own production behavior.

See `docs/compatibility_debt_report.md` and `docs/enterprise_readiness_gap_report.md` for removal conditions and blockers.

## Documentation

- `docs/architecture_v3.md`
- `docs/compatibility_debt_report.md`
- `docs/enterprise_readiness_gap_report.md`
- `docs/operational_runtime_observability.md`
- `docs/runtime_readiness.md`
- `docs/external_integration_governance.md`
- `docs/platform_catalog_governance.md`
- `docs/catalog_control_plane_runbook.md`
- `docs/catalog_failure_drills.md`
- `docs/catalog_contract_schema_lifecycle.md`
- `docs/catalog_live_certification_evidence.md`
- `docs/catalog_evidence_integrity.md`
- `docs/catalog_artifact_trust_and_retention.md`
- `docs/catalog_release_gates.md`
- `docs/catalog_promotion_manifest_provenance.md`
- `docs/catalog_promotion_audit_dossier.md`
- `docs/catalog_control_plane_slos_alerts.md`
- `docs/catalog_emergency_path_policy.md`
- `docs/module_package_classification.md`
- `docs/deep_analytics_validation_boundary.md`
- `docs/cleanup_report.md`
- `docs/WRITE_PATH_AUDIT.md`

## License

Internal / proprietary.
