# Patch 18 (EN) - Enterprise End-to-End Hardening

This patch pack is designed to be run sequentially, one YAML file at a time.

## Why Patch 18?
Patch 17 fixed most of the enterprise gaps, but several critical items remain:
- Trust boundary: roles must be claims-only (header roles are a privilege-escalation vector).
- JWKS retrieval: current implementation blocks request threads and can crash on fetch errors.
- Error taxonomy: tool/schema validation still collapses to ChatFailed, losing machine-stable codes.
- Retention: SQL logs need a purge mechanism aligned with Observability:RetentionDays.
- Secrets hygiene: avoid committing real connection strings; add local override pattern.
- Ops readiness: health/ready checks and request limits.
- CI: add cross-platform matrix and minimal security checks.

## How to run
Copy `spec/patch_18/` into the repo `spec/` folder, then run each YAML file in order with your Agent.
Update `spec/PROGRESS.md` after each patch.

## Notes
- Patch 18 is intentionally strict. If a requirement conflicts with existing behavior, prefer security and stability.
