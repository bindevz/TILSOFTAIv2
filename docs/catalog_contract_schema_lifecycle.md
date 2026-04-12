# Catalog Contract Schema Lifecycle - Sprint 11

Capability argument contracts are mandatory production metadata.

## Contract Fields

`ArgumentContract` now supports:

- `ContractVersion`: required lifecycle version, default `1`.
- `SchemaDialect`: optional schema dialect when a schema reference is used.
- `SchemaRef`: optional external schema reference for future JSON Schema interop.
- `RequiredArguments`, `AllowedArguments`, `AllowAdditionalArguments`, and typed `Arguments`.

If `SchemaRef` is supplied, `SchemaDialect` must also be supplied.

## Versioning Rules

- Increment `ContractVersion` when the accepted argument shape changes.
- Keep backward-compatible additions only when `AllowAdditionalArguments=true` or the new argument is optional and listed in `AllowedArguments`.
- Treat removing or renaming arguments as a breaking contract change.
- Record the contract version in catalog change notes.

## JSON Schema Interop Direction

The current runtime enforces deterministic first-party typed rules. JSON Schema interop should be additive:

- `SchemaDialect` identifies the schema family, for example `json-schema-draft-2020-12`.
- `SchemaRef` points to a governed schema artifact.
- Runtime validation must continue to return deterministic operator-readable error codes.
- Schema artifacts must not contain secrets.

## Minimum Production Contract Standard

- No-argument capabilities must set `AllowAdditionalArguments=false`.
- String identifiers need format, min length, and max length.
- Enumerated values need explicit `Enum` lists.
- Numeric values need `Min` and/or `Max` when business bounds exist.
- Newly added catalog records must pass preview before submit.
