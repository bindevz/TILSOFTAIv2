# Patch 20 (EN) - Run Notes

- Run ONE yaml at a time in the order listed in 20_00_overview.yaml.
- After each yaml:
  1) dotnet build -c Release
  2) dotnet test -c Release
  3) Update spec/PROGRESS.md with THAT patch result
- Do NOT merge patches or reorder.
- Keep SQL scripts idempotent.
