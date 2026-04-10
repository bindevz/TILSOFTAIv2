# Sprint 4 Execution Pack for TILSOFTAIv2

This pack is designed for the repository **bindevz/TILSOFTAIv2** after Sprint 3.

## Why Sprint 4 exists

After Sprint 3, the repository has already achieved several important V3 milestones:
- `SupervisorRuntime` is real and routes requests.
- `AccountingAgent` and `WarehouseAgent` exist as domain agent skeletons.
- `LegacyChatPipelineBridge` exists as the shared compatibility path.
- `ApprovalEngine` and `IWriteActionGuard` govern write execution.
- `SqlToolAdapter` enforces approval-backed write execution.

However, the repository is still transitional.
The most important remaining gap is that domain agents are not yet truly domain-native in execution behavior.

Sprint 4 closes that gap by forcing one real path to exist.

## Sprint 4 target

Sprint 4 must deliver:
1. one real Warehouse native execution path
2. usable capability modeling for that path
3. end-to-end approval lifecycle tests
4. production-safe chat endpoint auth behavior
5. README/docs aligned with runtime truth

## Files in this pack

- `SPRINT_4_PROMPT.txt`
  - main implementation prompt for the coding agent
- `SPRINT_4_CHECKLIST.yaml`
  - step-by-step execution and scope control
- `SPRINT_4_DOD.yaml`
  - definition of done for validation
- `SPRINT_4_REVIEW_PROMPT.txt`
  - post-implementation review prompt

## Suggested usage

### Step 1
Use your standing master instruct for the repository.

### Step 2
Load:
- `SPRINT_4_PROMPT.txt`
- `SPRINT_4_CHECKLIST.yaml`
- `SPRINT_4_DOD.yaml`

### Step 3
After implementation, run:
- `SPRINT_4_REVIEW_PROMPT.txt`

## Important implementation discipline

Sprint 4 is **not** the sprint for adding more agents or speculative architecture.
It is the sprint for proving that the V3 model works in one real domain path.

The most important success signal is:
- `WarehouseAgent` handles at least one capability natively
- that path does not depend on `LegacyChatPipelineBridge`
- write paths remain governed through `ApprovalEngine`

## What not to do in Sprint 4

- do not add new agents
- do not add planners
- do not widen module-centric runtime
- do not keep auth weak at the edge
- do not rewrite the whole platform just to avoid one focused implementation

## Expected next step after Sprint 4

If Sprint 4 succeeds, Sprint 5 should focus on:
- second native domain path
- one non-SQL adapter implemented for real
- further compatibility debt reduction
- final production hardening
