# Package Shell Retirement

Runtime routing is supervisor-native, domain-agent-native, catalog-native, and adapter-native. The old Platform and Analytics package shells are no longer production routing owners and are no longer solution projects.

Sprint 21 removes `ITilsoftModule` and the remaining Platform/Analytics package shells. Provider/model execution concerns belong in infrastructure and tool adapters unless a real business-domain agent boundary exists.

## Classifications

| Former package shell | Sprint 21 result | Runtime meaning |
|----------------------|------------------|-----------------|
| Platform | Deleted | Native runtime and platform catalog own production platform capabilities. |
| Analytics | Deleted | Analytics orchestration remains in orchestration/infrastructure boundaries, not package loading. |

## Removed

The Model package was deleted in Sprint 19. Sprint 20 deleted the loader and scope resolver substrate. Sprint 21 deleted the final Platform/Analytics package shells.

## Operational Rules

- Default runtime configuration has no `Modules` section.
- `native-runtime` and `platform-catalog` are the readiness sources for production capability ownership.
- New production runtime capability records should be added to the platform catalog instead of package loaders.
- Remaining capability metadata should live in catalog/tool records.
- Any new production work that depends on package loader identity is rejected as compatibility drift.
- Do not add a technical model/provider module or pseudo-agent as a shortcut for execution ownership.
