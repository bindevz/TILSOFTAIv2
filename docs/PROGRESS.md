# Progress

- Created on 2026-01-30; awaiting patch tracking entries.
- 2026-01-30: Completed spec/patch_17/17_01_identity_consistency_for_errors.yaml.
  - Files: src/TILSOFTAI.Domain/Configuration/AuthOptions.cs; src/TILSOFTAI.Domain/Security/IdentityResolutionPolicy.cs; src/TILSOFTAI.Domain/TILSOFTAI.Domain.csproj; src/TILSOFTAI.Domain/Errors/ErrorCode.cs; src/TILSOFTAI.Infrastructure/Errors/InMemoryErrorCatalog.cs; src/TILSOFTAI.Api/Extensions/AddTilsoftAiExtensions.cs; src/TILSOFTAI.Api/Middlewares/ExecutionContextMiddleware.cs; src/TILSOFTAI.Api/Middlewares/ExceptionHandlingMiddleware.cs; tests/TILSOFTAI.Tests.Integration/TenantIsolationTests.cs; spec/patch_17/PROGRESS.md.
- 2026-01-30: Completed spec/patch_17/17_03_prompt_context_pack_budgeting.yaml.
  - Files: src/TILSOFTAI.Domain/Configuration/ToolCatalogContextPackOptions.cs; src/TILSOFTAI.Domain/Configuration/ConfigurationSectionNames.cs; src/TILSOFTAI.Api/Extensions/AddTilsoftAiExtensions.cs; src/TILSOFTAI.Api/appsettings.json; src/TILSOFTAI.Infrastructure/Prompting/ToolCatalogContextPackProvider.cs; src/TILSOFTAI.Orchestration/Prompting/ContextPackBudgeter.cs; tests/TILSOFTAI.Tests.Contract/PromptingContextPackBudgetTests.cs; spec/patch_17/PROGRESS.md.
- 2026-01-30: Completed spec/patch_17/17_05_repo_hygiene_and_progress.yaml.
  - Files: tools/verify-repo-clean.ps1; tools/verify-repo-clean.sh; .github/workflows/ci.yml; spec/PROGRESS.md; spec/patch_17/PROGRESS.md.
