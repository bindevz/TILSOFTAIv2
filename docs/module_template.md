# Module Template (Reusable) — NOT tied to Model

Mục tiêu: thêm module mới (logistics / sales / purchasing…) chỉ bằng cách:
1) thêm semantic views + ai_* SPs trong SQL  
2) seed ToolCatalog + schema JSON cho tool args  
3) thêm contract tests

## 1) Folder Layout
- sql/05_<module>/
  - views/
  - sps/
  - seeds/ (optional)
- src/**/Domain/<Module>/
  - ToolCatalogSeed.cs
  - Handlers/
  - Schemas/
- tests/<Module>/

## 2) Required Tool Set (minimum)
- ai_<module>_search (keyword, paging)
- ai_<module>_get_overview (id/code)
- ai_<module>_get_details (child entities)
- ai_<module>_compare (multi ids)
- ai_<module>_diagnostics (rule-driven)

## 3) JSON Envelope Contract
All tools must return:
- meta: tenantId, generatedAtUtc, rowCount, warnings
- columns: name, type, descriptionKey
- rows: list of objects

## 4) Governance
- Tools must be registered in ToolCatalog.
- Strict JSON schema validation for args.
- Tenant scoping is mandatory; unsafe queries rejected.

## 5) Write tools
- ai_<module>_create/update/delete => require ApprovalPolicy (human-in-the-loop).
