# Model Module - Enterprise Adapter

This directory contains the Enterprise implementation of the Model Module Adapter.

## Concept
The Model Module uses an Adapter Pattern to allow the same AI Capabilities (Tools) to operate on different schema versions (Demo vs Enterprise) without code changes in the C# layer.

- **Demo**: Uses `dbo.Model`, `dbo.ModelPiece`, etc. (located in `../model/`)
- **Enterprise**: Maps existing legacy tables (e.g. `ModelUD`, `ModelMaterialConfig`, `Season`) to the standard `vw_ModelSemantic` view and associated stored procedures.

## Implementation Instructions

To deploy the Enterprise version:
1. Ensure the `sql/02_modules/model` scripts (Tables) are NOT deployed/used, OR simply override the View/SPs.
2. Adapt `vw_ModelSemantic` to select from your enterprise tables (`ModelUD`, etc).
3. Ensure the columns match the contract: `TenantId, Language, ModelId, ModelCode, Name, TotalCbm, TotalWeightKg, LoadabilityIndex, Qnt40HC, PieceCount, BoxInSet`.
4. Implement the stored procedures in `002_sps_model_enterprise.sql` to verify against your schemas.

## Required Source Tables
- `Model` (or `ModelUD`)
- `ModelPiece`
- `ModelPackagingMethodOption`
- `ModelMaterialConfig`
