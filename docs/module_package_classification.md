# Module Package Classification - Sprint 11

Runtime routing is supervisor-native and capability-native. Remaining module packages are not production routing owners by default.

Sprint 11 decision: module packages are formally retained as non-runtime packaging or diagnostic artifacts only. They are not a future production capability ownership path.

## Classifications

| Package | Classification | Runtime meaning |
|---------|----------------|-----------------|
| `TILSOFTAI.Modules.Platform` | packaging-only | Retained as package structure; not a default runtime loader owner. |
| `TILSOFTAI.Modules.Model` | packaging-only | Retained as package structure; not a default runtime loader owner. |
| `TILSOFTAI.Modules.Analytics` | diagnostic-only | Retained for diagnostic/deep workflow boundaries; not part of `/health/ready`. |

## Operational Rules

- `Modules:EnableLegacyAutoload=false` remains the default.
- `ModuleHealthCheck` is diagnostic and reports the classification map.
- New production runtime capability records should be added to the platform catalog instead of module package loaders.
- Existing module package metadata should either move into catalog/tool records or stay explicitly classified as non-runtime packaging.
- Any new production work that depends on module loader identity is rejected as compatibility drift.
