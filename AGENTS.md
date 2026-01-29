# TILSOFTAI - Codex Instructions

## Source of truth
- Source of truth: spec/patch_fix/00_master_plan.yaml
- Apply specs in order: spec/patch_fix/PATCH_01... → PATCH_10... (as listed in 00_master_plan.yaml: spec_files.apply_in_order)

## How to work (must follow)
- At the start of every run/session:
  1) Open and read PATCH_01_*.yaml.
  2) Read spec_files.apply_in_order.
  3) Read docs/PROGRESS.md to find the next NOT-DONE spec (if PROGRESS.md missing, create it).
- Implement EXACTLY ONE spec per run: the next NOT-DONE spec only.
- After finishing the spec:
  - run build/tests (or the project’s standard verification command),
  - update docs/PROGRESS.md: mark the spec as DONE + note what changed (files/DB objects),
  - stop (do not continue to the next spec in the same run).