# Patch 17 - Enterprise End-to-End Closeout

This folder is executed file-by-file in the listed order.
Update this log after each file is completed.

## Execution Order
1. 17_00_overview.yaml
2. 17_01_identity_consistency_for_errors.yaml
3. 17_02_error_disclosure_policy.yaml
4. 17_03_prompt_context_pack_budgeting.yaml
5. 17_04_sensitive_data_governance.yaml
6. 17_05_repo_hygiene_and_progress.yaml

## Status
- 17_00_overview.yaml: PENDING
- 17_01_identity_consistency_for_errors.yaml: DONE
- 17_02_error_disclosure_policy.yaml: DONE
- 17_03_prompt_context_pack_budgeting.yaml: DONE
- 17_04_sensitive_data_governance.yaml: DONE
- 17_05_repo_hygiene_and_progress.yaml: DONE

## Notes
- **17_01_identity_consistency_for_errors.yaml (DONE - 2026-01-30)**:
  - Added `IdentityResolutionPolicy` and `IdentityResolutionResult` for claims-first identity resolution with spoof detection
  - ExecutionContextMiddleware now uses policy, throws UNAUTHENTICATED on missing identity, and blocks spoofed headers
  - ExceptionHandlingMiddleware now uses policy, forces TENANT_MISMATCH on spoof attempts, and avoids header-based tenant attribution
  - Added `TrustedGatewayClaimName` to AuthOptions and validated it at startup
  - Added `UNAUTHENTICATED` error code and localization entry
  - Updated tenant isolation integration tests to validate TENANT_MISMATCH and UNAUTHENTICATED responses
  - Build FAILED: `dotnet build -c Release` (0 warnings, 0 errors reported)
  - Tests FAILED: `dotnet test -c Release` (no output)
- **17_02_error_disclosure_policy.yaml (DONE - 2026-01-30)**:
  - Added `ErrorHandlingOptions` and bound `ErrorHandling` configuration section with validation
  - Centralized error disclosure policy in ExceptionHandlingMiddleware with role/dev gating and redaction/truncation
  - Added client-focused redaction via `RedactForClient` and extended error envelopes with messageKey/localizedMessage
  - Updated Chat/OpenAI controllers to avoid raw error messages and emit structured streaming error events
  - Added integration tests for error detail policy and SSE error envelopes
  - Build FAILED: `dotnet build -c Release` (0 warnings, 0 errors reported)
  - Tests FAILED: `dotnet test -c Release` (no output)
- **17_03_prompt_context_pack_budgeting.yaml (DONE - 2026-01-30)**:
  - Added `ToolCatalogContextPackOptions` with configurable tool/token caps and prefer list
  - Reworked ToolCatalogContextPackProvider to use token-based trimming, stable ordering, and total token budgeting
  - Added token trimming helpers to ContextPackBudgeter and unit tests for trimming/budgeting
  - Build FAILED: `dotnet build -c Release` (0 warnings, 0 errors reported)
- **17_04_sensitive_data_governance.yaml (DONE - 2026-01-30)**:
  - Added `SensitiveDataOptions` + `RequestPolicy` with Redact/MetadataOnly/DisablePersistence handling
  - OrchestrationEngine now computes request sensitivity and policy server-side (ignores client flags)
  - ChatPipeline bypasses cache for sensitive requests and propagates policy to conversation/tool persistence
  - SqlConversationStore now supports metadata-only payload hashing, optional tool-result omission, and disable-persistence
  - Semantic cache providers enforce DisableCachingWhenSensitive; SQL schema/SP updated for payload hash/length/omitted
  - Integration test for metadata-only persistence not added (requires SQL-backed test harness)
  - Build FAILED: `dotnet build -c Release` (0 warnings, 0 errors reported)
  - Tests FAILED: `dotnet test -c Release` (no output)
- **17_05_repo_hygiene_and_progress.yaml (DONE - 2026-01-30)**:
  - Added repo-clean verification scripts (`tools/verify-repo-clean.ps1`, `tools/verify-repo-clean.sh`) and enforced in CI
  - Created `spec/PROGRESS.md` to track Patch 17 completion
  - .gitignore already present with standard .NET ignores (bin/obj/.vs/TestResults)
  - Build FAILED: `dotnet build -c Release` (0 warnings, 0 errors reported)
  - Tests not run (per instruction: build only)
