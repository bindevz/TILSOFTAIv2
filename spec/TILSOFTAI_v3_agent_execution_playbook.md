# TILSOFTAI V3 Agent Execution Playbook

This document is intended for an AI coding agent or an engineer supervising one.

## Primary objective

Refactor the repository from V2-style module-centric orchestration into V3 agent-centric architecture without discarding useful infrastructure.

## Work mode

- Work in small, reviewable increments.
- Keep the codebase buildable as often as possible.
- Migrate first, delete second.
- Do not delete legacy behavior before replacement exists.

## Mandatory order of attack

1. Stabilize config, auth, and docs drift
2. Introduce V3 abstractions
3. Wrap current SQL execution with adapter contracts
4. Add first domain agents
5. Promote approval flow into Approval Engine
6. Replace module-first runtime ownership with capability packs
7. Add non-SQL adapters
8. Remove obsolete code
9. Rewrite README and architecture docs

## Decisions to keep

Keep and reuse where still valuable:
- execution context
- observability
- rate limiting
- structured logging
- schema validation
- resilience policies
- streaming infrastructure
- SQL execution core
- approval validation logic

## Decisions to change

- orchestration center must become SupervisorRuntime
- runtime ownership must move to Domain Agents
- generic integration must move to Tool Adapters
- write path must move to Approval Engine
- modules must stop being the main runtime abstraction

## Required reports from the agent after each slice

- summary of changes
- files touched
- new abstractions introduced
- legacy abstractions deprecated
- remaining debt
- risks introduced
- tests added or updated

## Stop conditions

The agent must stop and report if:
- tenant safety becomes uncertain
- approval path is weakened
- auth becomes less strict than before
- there is uncertainty about secret handling
- there is a need to introduce free-form production SQL
