# Module Package Classification - Sprint 19

Runtime routing is supervisor-native, domain-agent-native, and capability-native. Module packages are not production routing owners.

Sprint 19 removes the obsolete Model module. Provider/model execution concerns belong in infrastructure and tool adapters unless a real business-domain agent boundary exists.

## Classifications

| Package | Classification | Runtime meaning |
|---------|----------------|-----------------|
| `TILSOFTAI.Modules.Platform` | packaging-only | Retained as bounded package metadata; not a default runtime loader owner and not a production capability ownership path. |
| `TILSOFTAI.Modules.Analytics` | diagnostic-only | Retained for external/deep analytics validation boundaries; not part of `/health/ready` and not general production routing ownership. |

## Removed

The Model module was deleted in Sprint 19. It is not a retained package, not a runtime owner, and not a valid future ownership boundary.

## Operational Rules

- `Modules:EnableLegacyAutoload=false` remains the default.
- `ModuleHealthCheck` is diagnostic and reports the classification map.
- New production runtime capability records should be added to the platform catalog instead of module package loaders.
- Remaining package metadata should either move into catalog/tool records or stay explicitly classified as non-runtime packaging or diagnostics.
- Any new production work that depends on module loader identity is rejected as compatibility drift.
- Do not add a technical model/provider module or pseudo-agent as a shortcut for execution ownership.
