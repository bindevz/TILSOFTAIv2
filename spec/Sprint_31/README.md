# TILSOFTAIv2 Agent Refactor Plan — English Phase Packet

## Purpose

This packet is written for Codex Agent or another coding agent that will refactor `bindevz/TILSOFTAIv2` to use **Microsoft Agent Framework** as the intelligent layer for **tool selection** and **parameter extraction**.

The refactor must keep the existing ERP safety foundation: capability catalog, SQL stored procedure whitelist, argument validator, tenant/role policy, approval engine, audit/telemetry, and adapter boundary.

## How to use this packet

Do **not** load every file into a single coding session. Work phase by phase.

Recommended order:

1. `00_ARCHITECTURE_AND_RULES.md`
2. `01_SQL_SERVER_2025_SEMANTIC_KB.md`
3. `02_AGENT_FRAMEWORK_TOOL_ROUTER.md`
4. `03_DYNAMIC_FUNCTION_TOOLS_AND_DESCRIPTIONS.md`
5. `04_CAPABILITY_EXECUTION_FACADE.md`
6. `05_ANSWER_COMPOSER.md`
7. `06_WRITE_PREVIEW_CONFIRMATION.md`
8. `07_COMPOSITE_CAPABILITIES.md`
9. `08_EVAL_OBSERVABILITY_AND_ROLLOUT.md`

Each phase has its own scope, tasks, implementation notes, and acceptance criteria. Finish one phase, run build/tests, then continue.

## Target runtime

```text
API / ChatHub / OpenAI-compatible endpoint
  -> ISupervisorRuntime
  -> MicrosoftAgentToolRouter
      -> hard signal extraction
      -> SQL Server 2025 semantic KB retrieval
      -> domain-gated candidate tools
      -> Microsoft Agent Framework tool calling
  -> CapabilityExecutionFacade
      -> tenant/role policy
      -> entity resolution
      -> argument validation
      -> approval guard
      -> adapter execution
  -> AnswerComposer
      -> raw JSON OR structured answer
```

## Core principle

Microsoft Agent Framework is the **brain for choosing tools and parameters**.

SQL Server 2025 is the **semantic memory/control plane**.

The existing TILSOFTAI governance layer remains the **execution safety boundary**.

## Non-negotiable rules

- Never let the model generate SQL.
- Never expose the full ERP tool catalog to the model.
- Never let a model-facing function call SQL directly.
- Never execute write/create/update/delete operations without user confirmation and approval.
- Never trust model-generated arguments without validation.
- Never leak sensitive fields into AI summarization.
- Do not hardcode production function descriptions in C#.
- Do not reintroduce a technical AI model/provider module if the ERP has a business domain named `model`; use `erp_model`, `product_model`, or `modeling` in code/catalog.

## Phase summary

| Phase | Goal | Output |
|---|---|---|
| 0 | Architecture and feature-flagged integration | Non-breaking entry point in `SupervisorRuntime` |
| 1 | SQL Server 2025 semantic KB | Catalog/description/embedding/entity alias storage |
| 2 | Agent Framework tool router | Candidate-gated agent tool calling |
| 3 | Dynamic function tools | Runtime descriptions and schemas from SQL KB |
| 4 | Capability execution facade | Centralized safe execution boundary |
| 5 | Answer composer | Raw JSON and structured answer modes |
| 6 | Write preview/confirmation | Safe create/update/delete through approval |
| 7 | Composite capabilities | Controlled multi-proc read aggregation |
| 8 | Eval, observability, rollout | Metrics, regression tests, safe deployment |
