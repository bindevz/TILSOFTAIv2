# TILSOFTAI V3 Instruct Bundle

This bundle is intended to be loaded into an AI coding agent as the authoritative instruction pack for the V3 upgrade.

## Included files

- `TILSOFTAI_v3_agent_instruct_master.yaml`  
  Primary instruction file. Load this first.

- `TILSOFTAI_v3_interfaces.yaml`  
  Core contract definitions to guide interface-first refactoring.

- `TILSOFTAI_v3_migration_tasks.yaml`  
  Ordered migration task plan.

- `TILSOFTAI_v3_cleanup_checklist.yaml`  
  Cleanup and deletion criteria for legacy V2 code.

- `README_v3.md`  
  Replacement README aligned with the V3 architecture.

- `TILSOFTAI_v3_agent_execution_playbook.md`  
  Practical execution playbook for the AI agent.

- `folder_tree_v3.md`  
  Proposed target project structure.

## Recommended usage order

1. Load `TILSOFTAI_v3_agent_instruct_master.yaml`
2. Load `TILSOFTAI_v3_interfaces.yaml`
3. Load `TILSOFTAI_v3_migration_tasks.yaml`
4. Load `TILSOFTAI_v3_cleanup_checklist.yaml`
5. Use `README_v3.md` as the target repository README
6. Use `TILSOFTAI_v3_agent_execution_playbook.md` as a behavior guide
7. Use `folder_tree_v3.md` during project layout refactor
