SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.DatasetCatalog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DatasetCatalog
    (
        Id bigint IDENTITY(1,1) NOT NULL,
        DatasetKey nvarchar(200) NOT NULL,
        TenantId nvarchar(50) NULL,
        BaseObject nvarchar(200) NOT NULL,
        TimeColumn nvarchar(200) NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_DatasetCatalog_IsEnabled DEFAULT (1),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DatasetCatalog_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_DatasetCatalog_Id PRIMARY KEY (Id)
    );
END;
GO

IF OBJECT_ID('dbo.FieldCatalog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FieldCatalog
    (
        DatasetKey nvarchar(200) NOT NULL,
        FieldKey nvarchar(200) NOT NULL,
        PhysicalColumn nvarchar(200) NOT NULL,
        DataType nvarchar(50) NOT NULL,
        IsMetric bit NOT NULL CONSTRAINT DF_FieldCatalog_IsMetric DEFAULT (0),
        IsDimension bit NOT NULL CONSTRAINT DF_FieldCatalog_IsDimension DEFAULT (0),
        AllowedAggregations nvarchar(200) NULL,
        IsFilterable bit NOT NULL CONSTRAINT DF_FieldCatalog_IsFilterable DEFAULT (1),
        IsGroupable bit NOT NULL CONSTRAINT DF_FieldCatalog_IsGroupable DEFAULT (1),
        IsSortable bit NOT NULL CONSTRAINT DF_FieldCatalog_IsSortable DEFAULT (1),
        IsEnabled bit NOT NULL CONSTRAINT DF_FieldCatalog_IsEnabled DEFAULT (1),
        CONSTRAINT PK_FieldCatalog PRIMARY KEY (DatasetKey, FieldKey)
    );
END;
GO

IF OBJECT_ID('dbo.EntityGraphCatalog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntityGraphCatalog
    (
        GraphKey nvarchar(200) NOT NULL,
        FromDatasetKey nvarchar(200) NOT NULL,
        ToDatasetKey nvarchar(200) NOT NULL,
        JoinType nvarchar(50) NOT NULL,
        JoinConditionTemplate nvarchar(2000) NOT NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_EntityGraphCatalog_IsEnabled DEFAULT (1),
        CONSTRAINT PK_EntityGraphCatalog PRIMARY KEY (GraphKey)
    );
END;
GO
