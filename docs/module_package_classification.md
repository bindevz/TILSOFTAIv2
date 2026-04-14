# Module Package Classification - Sprint 20

Runtime routing is supervisor-native, domain-agent-native, catalog-native, and adapter-native. Module packages are not production routing owners and are not referenced by the production API project.

Sprint 20 removes active module autoload, module health, module activation options, and module scope resolution from API runtime. Provider/model execution concerns belong in infrastructure and tool adapters unless a real business-domain agent boundary exists.

## Classifications

| Package | Classification | Runtime meaning |
|---------|----------------|-----------------|
| `TILSOFTAI.Modules.Platform` | solution-local compatibility package | Not referenced by API startup and not a production capability ownership path. |
| `TILSOFTAI.Modules.Analytics` | solution-local diagnostic package | Not referenced by API startup, not part of `/health/ready`, and not general production routing ownership. |

## Removed

The Model module was deleted in Sprint 19. Sprint 20 deleted the remaining loader and scope resolver substrate. Neither is a retained runtime owner or valid future ownership boundary.

## Operational Rules

- Default runtime configuration has no `Modules` section.
- `native-runtime` and `platform-catalog` are the readiness sources for production capability ownership.
- New production runtime capability records should be added to the platform catalog instead of package loaders.
- Remaining package metadata should either move into catalog/tool records or stay explicitly classified as non-runtime packaging or diagnostics.
- Any new production work that depends on module loader identity is rejected as compatibility drift.
- Do not add a technical model/provider module or pseudo-agent as a shortcut for execution ownership.
