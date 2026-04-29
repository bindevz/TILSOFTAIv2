SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT N'Optional seed: upserts clearly marked test-only model data for the local no-auth default tenant.';
GO

DECLARE @TenantId nvarchar(50) = N'default';
DECLARE @Lang nvarchar(10) = N'en';

MERGE dbo.Material AS target
USING (VALUES
    (N'MAT-OAK', N'Oak Wood', N'Wood', N'Frame', N'test-only seeded model data; not ERP production data', CONVERT(decimal(18,4), 750)),
    (N'MAT-STL', N'Stainless Steel', N'Metal', N'Hardware', N'test-only seeded model data; not ERP production data', CONVERT(decimal(18,4), 7850)),
    (N'MAT-LIN', N'Linen Fabric', N'Fabric', N'Upholstery', N'test-only seeded model data; not ERP production data', CONVERT(decimal(18,4), 0)),
    (N'MAT-FOAM', N'High Density Foam', N'Foam', N'Upholstery', N'test-only seeded model data; not ERP production data', CONVERT(decimal(18,4), 50))
) AS source (MaterialCode, Name, Category, Section, Description, DensityKgPerM3)
ON target.TenantId = @TenantId AND target.MaterialCode = source.MaterialCode
WHEN MATCHED THEN UPDATE SET
    Language = @Lang,
    Name = source.Name,
    Category = source.Category,
    Section = source.Section,
    Description = source.Description,
    DensityKgPerM3 = source.DensityKgPerM3,
    UpdatedAtUtc = sysutcdatetime()
WHEN NOT MATCHED THEN INSERT
    (TenantId, Language, MaterialCode, Name, Category, Section, Description, DensityKgPerM3)
    VALUES (@TenantId, @Lang, source.MaterialCode, source.Name, source.Category, source.Section, source.Description, source.DensityKgPerM3);

MERGE dbo.Model AS target
USING (VALUES
    (N'ABC', N'Acceptance Chair', N'test-only seeded model data; not ERP production data', CONVERT(decimal(18,4), 0.1500), CONVERT(decimal(18,4), 8.5000), 1, 450),
    (N'XYZ', N'Acceptance Table', N'test-only seeded model data; not ERP production data', CONVERT(decimal(18,4), 0.4500), CONVERT(decimal(18,4), 45.0000), 1, 140),
    (N'SET-DINING-001', N'Acceptance Dining Set', N'test-only seeded model data; not ERP production data', CONVERT(decimal(18,4), 1.0500), CONVERT(decimal(18,4), 79.0000), 5, 60)
) AS source (ModelCode, Name, Description, TotalCbm, TotalWeightKg, PieceCount, Qnt40HC)
ON target.TenantId = @TenantId AND target.ModelCode = source.ModelCode
WHEN MATCHED THEN UPDATE SET
    Language = @Lang,
    Name = source.Name,
    Description = source.Description,
    TotalCbm = source.TotalCbm,
    TotalWeightKg = source.TotalWeightKg,
    PieceCount = source.PieceCount,
    Qnt40HC = source.Qnt40HC,
    UpdatedAtUtc = sysutcdatetime()
WHEN NOT MATCHED THEN INSERT
    (TenantId, Language, ModelCode, Name, Description, TotalCbm, TotalWeightKg, PieceCount, Qnt40HC)
    VALUES (@TenantId, @Lang, source.ModelCode, source.Name, source.Description, source.TotalCbm, source.TotalWeightKg, source.PieceCount, source.Qnt40HC);

DECLARE @MatWood int = (SELECT MaterialId FROM dbo.Material WHERE TenantId = @TenantId AND MaterialCode = N'MAT-OAK');
DECLARE @MatSteel int = (SELECT MaterialId FROM dbo.Material WHERE TenantId = @TenantId AND MaterialCode = N'MAT-STL');
DECLARE @MatFabric int = (SELECT MaterialId FROM dbo.Material WHERE TenantId = @TenantId AND MaterialCode = N'MAT-LIN');
DECLARE @MatFoam int = (SELECT MaterialId FROM dbo.Material WHERE TenantId = @TenantId AND MaterialCode = N'MAT-FOAM');
DECLARE @Chair int = (SELECT ModelId FROM dbo.Model WHERE TenantId = @TenantId AND ModelCode = N'ABC');
DECLARE @Table int = (SELECT ModelId FROM dbo.Model WHERE TenantId = @TenantId AND ModelCode = N'XYZ');
DECLARE @Set int = (SELECT ModelId FROM dbo.Model WHERE TenantId = @TenantId AND ModelCode = N'SET-DINING-001');

MERGE dbo.ModelMaterial AS target
USING (VALUES
    (@Chair, @MatWood, N'Frame', CONVERT(decimal(18,4), 0.0200), N'm3', CONVERT(decimal(18,4), 5.0000)),
    (@Chair, @MatFabric, N'Seat', CONVERT(decimal(18,4), 1.5000), N'm2', CONVERT(decimal(18,4), 0.5000)),
    (@Chair, @MatFoam, N'Seat', CONVERT(decimal(18,4), 0.0500), N'm3', CONVERT(decimal(18,4), 1.0000)),
    (@Table, @MatWood, N'Top', CONVERT(decimal(18,4), 0.1500), N'm3', CONVERT(decimal(18,4), 40.0000)),
    (@Table, @MatSteel, N'Hardware', CONVERT(decimal(18,4), 2.0000), N'kg', CONVERT(decimal(18,4), 2.0000))
) AS source (ModelId, MaterialId, Section, Quantity, Unit, WeightKg)
ON target.TenantId = @TenantId AND target.ModelId = source.ModelId AND target.MaterialId = source.MaterialId AND target.Section = source.Section
WHEN MATCHED THEN UPDATE SET
    Quantity = source.Quantity,
    Unit = source.Unit,
    WeightKg = source.WeightKg,
    UpdatedAtUtc = sysutcdatetime()
WHEN NOT MATCHED THEN INSERT
    (TenantId, ModelId, MaterialId, Section, Quantity, Unit, WeightKg)
    VALUES (@TenantId, source.ModelId, source.MaterialId, source.Section, source.Quantity, source.Unit, source.WeightKg);

MERGE dbo.ModelPiece AS target
USING (VALUES
    (@Set, N'Dining Table', 1, @Table, 1),
    (@Set, N'Dining Chair', 4, @Chair, 2)
) AS source (ModelId, PieceName, Quantity, ChildModelId, Sequence)
ON target.TenantId = @TenantId AND target.ModelId = source.ModelId AND target.PieceName = source.PieceName AND target.Sequence = source.Sequence
WHEN MATCHED THEN UPDATE SET
    Quantity = source.Quantity,
    ChildModelId = source.ChildModelId,
    UpdatedAtUtc = sysutcdatetime()
WHEN NOT MATCHED THEN INSERT
    (TenantId, ModelId, PieceName, Quantity, ChildModelId, Sequence)
    VALUES (@TenantId, source.ModelId, source.PieceName, source.Quantity, source.ChildModelId, source.Sequence);

MERGE dbo.ModelPackagingOption AS target
USING (VALUES
    (@Chair, N'Standard Box', N'Carton', 2, CONVERT(decimal(18,4), 0.3200), CONVERT(decimal(18,4), 18.0000), NULL, 450),
    (@Table, N'Flat Pack', N'Carton', 1, CONVERT(decimal(18,4), 0.4800), CONVERT(decimal(18,4), 48.0000), NULL, 140),
    (@Set, N'Set Consolidation', N'Carton', 1, CONVERT(decimal(18,4), 1.1500), CONVERT(decimal(18,4), 85.0000), NULL, 60)
) AS source (ModelId, OptionName, PackagingType, UnitsPerCarton, CartonCbm, CartonWeightKg, LoadabilityIndex, Qnt40HC)
ON target.TenantId = @TenantId AND target.ModelId = source.ModelId AND target.OptionName = source.OptionName
WHEN MATCHED THEN UPDATE SET
    PackagingType = source.PackagingType,
    UnitsPerCarton = source.UnitsPerCarton,
    CartonCbm = source.CartonCbm,
    CartonWeightKg = source.CartonWeightKg,
    LoadabilityIndex = source.LoadabilityIndex,
    Qnt40HC = source.Qnt40HC,
    UpdatedAtUtc = sysutcdatetime()
WHEN NOT MATCHED THEN INSERT
    (TenantId, ModelId, OptionName, PackagingType, UnitsPerCarton, CartonCbm, CartonWeightKg, LoadabilityIndex, Qnt40HC)
    VALUES (@TenantId, source.ModelId, source.OptionName, source.PackagingType, source.UnitsPerCarton, source.CartonCbm, source.CartonWeightKg, source.LoadabilityIndex, source.Qnt40HC);

PRINT N'Upserted test-only seeded model data for tenant default; not ERP production data.';
GO
