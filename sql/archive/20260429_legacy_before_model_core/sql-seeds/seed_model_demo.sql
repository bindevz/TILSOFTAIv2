/*
Seed data for Model domain (demo / dev). 
- Idempotent (safe to run multiple times)
- Uses ModelUD as business key for lookup
- Tries to insert into optional lookup tables if they exist

NOTE: Model is only an example module. Do NOT treat these values as business truth.

GUARD: This script requires ERP Model tables with specific columns. 
If the dbo.Model table doesn't exist or has different schema, the script is skipped.
*/

SET NOCOUNT ON;

-- Check if required ERP Model table and columns exist
IF OBJECT_ID('dbo.Model', 'U') IS NULL
BEGIN
    PRINT 'dbo.Model table not found. Skip seeding Model domain (ERP tables required).';
END
ELSE IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Model' AND COLUMN_NAME = 'ModelUD')
BEGIN
    PRINT 'dbo.Model does not have ModelUD column (different schema). Skip seeding.';
END
ELSE
BEGIN
    -- All guards passed - execute seed via sp_executesql to defer column validation
    PRINT 'Seeding Model domain...';
    
    DECLARE @sql NVARCHAR(MAX) = N'
--------------------------------------------------------------------------------
-- 0) Helper: ensure core rows exist in dependent tables (optional)
--------------------------------------------------------------------------------
IF OBJECT_ID(''dbo.PackagingMethod'', ''U'') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.PackagingMethod WHERE PackagingMethodID = 1)
        INSERT dbo.PackagingMethod (PackagingMethodID) VALUES (1);
END

IF OBJECT_ID(''dbo.MaterialGroup'', ''U'') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.MaterialGroup WHERE MaterialGroupID = 1)
        INSERT dbo.MaterialGroup (MaterialGroupID) VALUES (1);
    IF NOT EXISTS (SELECT 1 FROM dbo.MaterialGroup WHERE MaterialGroupID = 2)
        INSERT dbo.MaterialGroup (MaterialGroupID) VALUES (2);
END

--------------------------------------------------------------------------------
-- 1) ProductWizardSection (materials sections)
--------------------------------------------------------------------------------
IF OBJECT_ID(''dbo.ProductWizardSection'', ''U'') IS NOT NULL
BEGIN
    MERGE dbo.ProductWizardSection AS t
    USING (VALUES
        (N''WOOD'', 1, 1, 10),
        (N''FABRIC'', 2, 0, 20),
        (N''PACKAGING'', 0, 0, 30)
    ) AS s(ProductWizardSectionNM, IsFSCEnabled, IsRCSEnabled, DisplayOrder)
    ON (t.ProductWizardSectionNM = s.ProductWizardSectionNM)
    WHEN NOT MATCHED THEN
        INSERT (ProductWizardSectionNM, IsFSCEnabled, IsRCSEnabled, DisplayOrder)
        VALUES (s.ProductWizardSectionNM, s.IsFSCEnabled, s.IsRCSEnabled, s.DisplayOrder);
END

--------------------------------------------------------------------------------
-- 2) Model (3 leaf models + 1 set model)
--------------------------------------------------------------------------------
DECLARE @M01 INT, @M02 INT, @M03 INT, @S01 INT;

-- Upsert by ModelUD
IF NOT EXISTS (SELECT 1 FROM dbo.Model WHERE ModelUD=''M01'')
    INSERT dbo.Model (ModelUD, ModelNM, Season, RangeName) VALUES (''M01'', ''Chair - Oak'', ''24/25'', ''DEMO'');
IF NOT EXISTS (SELECT 1 FROM dbo.Model WHERE ModelUD=''M02'')
    INSERT dbo.Model (ModelUD, ModelNM, Season, RangeName) VALUES (''M02'', ''Chair - Walnut'', ''24/25'', ''DEMO'');
IF NOT EXISTS (SELECT 1 FROM dbo.Model WHERE ModelUD=''M03'')
    INSERT dbo.Model (ModelUD, ModelNM, Season, RangeName) VALUES (''M03'', ''Table - Oak'', ''24/25'', ''DEMO'');
IF NOT EXISTS (SELECT 1 FROM dbo.Model WHERE ModelUD=''S01'')
    INSERT dbo.Model (ModelUD, ModelNM, Season, RangeName) VALUES (''S01'', ''Dining Set (M01+M03)'', ''24/25'', ''DEMO'');

SELECT @M01 = ModelID FROM dbo.Model WHERE ModelUD=''M01'';
SELECT @M02 = ModelID FROM dbo.Model WHERE ModelUD=''M02'';
SELECT @M03 = ModelID FROM dbo.Model WHERE ModelUD=''M03'';
SELECT @S01 = ModelID FROM dbo.Model WHERE ModelUD=''S01'';

--------------------------------------------------------------------------------
-- 3) ModelPiece (S01 is SET of M01 x4 + M03 x1)
--------------------------------------------------------------------------------
IF OBJECT_ID(''dbo.ModelPiece'', ''U'') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.ModelPiece WHERE ModelID=@S01 AND PieceModelID=@M01)
        INSERT dbo.ModelPiece (ModelID, PieceModelID, Quantity, RowIndex) VALUES (@S01, @M01, 4, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ModelPiece WHERE ModelID=@S01 AND PieceModelID=@M03)
        INSERT dbo.ModelPiece (ModelID, PieceModelID, Quantity, RowIndex) VALUES (@S01, @M03, 1, 2);
END

--------------------------------------------------------------------------------
-- 4) Packaging options (default)
--------------------------------------------------------------------------------
IF OBJECT_ID(''dbo.ModelPackagingMethodOption'', ''U'') IS NOT NULL
BEGIN
    -- M01 default
    IF NOT EXISTS (SELECT 1 FROM dbo.ModelPackagingMethodOption WHERE ModelID=@M01 AND IsDefault=1)
        INSERT dbo.ModelPackagingMethodOption
            (ModelID, PackagingMethodID, IsDefault, Description, CartonBoxDimL, CartonBoxDimW, CartonBoxDimH,
             Qnt20DC, Qnt40DC, Qnt40HC, NetWeight, GrossWeight, CBM, BoxInSet, MethodCode, PackagingMethodOptionUD)
        VALUES
            (@M01, 1, 1, ''Default carton'', ''600'', ''550'', ''900'', 80, 160, 200, ''10.0'', ''12.0'', ''0.297'', 1, ''CTN'', ''A'');

    -- M02 default (slightly worse loading)
    IF NOT EXISTS (SELECT 1 FROM dbo.ModelPackagingMethodOption WHERE ModelID=@M02 AND IsDefault=1)
        INSERT dbo.ModelPackagingMethodOption
            (ModelID, PackagingMethodID, IsDefault, Description, CartonBoxDimL, CartonBoxDimW, CartonBoxDimH,
             Qnt20DC, Qnt40DC, Qnt40HC, NetWeight, GrossWeight, CBM, BoxInSet, MethodCode, PackagingMethodOptionUD)
        VALUES
            (@M02, 1, 1, ''Default carton'', ''650'', ''600'', ''950'', 70, 140, 180, ''10.5'', ''12.5'', ''0.371'', 1, ''CTN'', ''A'');

    -- M03 default
    IF NOT EXISTS (SELECT 1 FROM dbo.ModelPackagingMethodOption WHERE ModelID=@M03 AND IsDefault=1)
        INSERT dbo.ModelPackagingMethodOption
            (ModelID, PackagingMethodID, IsDefault, Description, CartonBoxDimL, CartonBoxDimW, CartonBoxDimH,
             Qnt20DC, Qnt40DC, Qnt40HC, NetWeight, GrossWeight, CBM, BoxInSet, MethodCode, PackagingMethodOptionUD)
        VALUES
            (@M03, 1, 1, ''Default carton'', ''1200'', ''800'', ''200'', 30, 60, 75, ''18.0'', ''21.0'', ''0.192'', 1, ''CTN'', ''A'');

    -- S01 default (set carton)
    IF NOT EXISTS (SELECT 1 FROM dbo.ModelPackagingMethodOption WHERE ModelID=@S01 AND IsDefault=1)
        INSERT dbo.ModelPackagingMethodOption
            (ModelID, PackagingMethodID, IsDefault, Description, CartonBoxDimL, CartonBoxDimW, CartonBoxDimH,
             Qnt20DC, Qnt40DC, Qnt40HC, NetWeight, GrossWeight, CBM, BoxInSet, MethodCode, PackagingMethodOptionUD)
        VALUES
            (@S01, 1, 1, ''Set carton'', ''1400'', ''900'', ''450'', 12, 24, 30, ''60.0'', ''68.0'', ''0.567'', 1, ''CTN'', ''A'');
END

--------------------------------------------------------------------------------
-- 5) Materials config (map model -> sections)
--------------------------------------------------------------------------------
IF OBJECT_ID(''dbo.ModelMaterialConfig'', ''U'') IS NOT NULL AND OBJECT_ID(''dbo.ProductWizardSection'', ''U'') IS NOT NULL
BEGIN
    DECLARE @SEC_WOOD INT = (SELECT TOP 1 ProductWizardSectionID FROM dbo.ProductWizardSection WHERE ProductWizardSectionNM=N''WOOD'');
    DECLARE @SEC_FABRIC INT = (SELECT TOP 1 ProductWizardSectionID FROM dbo.ProductWizardSection WHERE ProductWizardSectionNM=N''FABRIC'');

    IF @SEC_WOOD IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ModelMaterialConfig WHERE ModelID=@M01 AND ProductWizardSectionID=@SEC_WOOD)
            INSERT dbo.ModelMaterialConfig (ModelID, ProductWizardSectionID) VALUES (@M01, @SEC_WOOD);
        IF NOT EXISTS (SELECT 1 FROM dbo.ModelMaterialConfig WHERE ModelID=@M02 AND ProductWizardSectionID=@SEC_WOOD)
            INSERT dbo.ModelMaterialConfig (ModelID, ProductWizardSectionID) VALUES (@M02, @SEC_WOOD);
        IF NOT EXISTS (SELECT 1 FROM dbo.ModelMaterialConfig WHERE ModelID=@M03 AND ProductWizardSectionID=@SEC_WOOD)
            INSERT dbo.ModelMaterialConfig (ModelID, ProductWizardSectionID) VALUES (@M03, @SEC_WOOD);
        IF NOT EXISTS (SELECT 1 FROM dbo.ModelMaterialConfig WHERE ModelID=@S01 AND ProductWizardSectionID=@SEC_WOOD)
            INSERT dbo.ModelMaterialConfig (ModelID, ProductWizardSectionID) VALUES (@S01, @SEC_WOOD);
    END

    IF @SEC_FABRIC IS NOT NULL
    BEGIN
        -- only chairs have fabric
        IF NOT EXISTS (SELECT 1 FROM dbo.ModelMaterialConfig WHERE ModelID=@M01 AND ProductWizardSectionID=@SEC_FABRIC)
            INSERT dbo.ModelMaterialConfig (ModelID, ProductWizardSectionID) VALUES (@M01, @SEC_FABRIC);
        IF NOT EXISTS (SELECT 1 FROM dbo.ModelMaterialConfig WHERE ModelID=@M02 AND ProductWizardSectionID=@SEC_FABRIC)
            INSERT dbo.ModelMaterialConfig (ModelID, ProductWizardSectionID) VALUES (@M02, @SEC_FABRIC);
    END
END

PRINT ''Seed Model domain completed.'';
';

    EXEC sp_executesql @sql;
END
GO
