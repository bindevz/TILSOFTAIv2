-- ============================================================
-- Patch 35.01: RuntimePolicy table
-- Policy-as-Data: operational knobs live in SQL, not appsettings.
-- Supports per-tenant, per-module, per-app, per-env overrides.
-- ============================================================

IF OBJECT_ID('dbo.RuntimePolicy', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RuntimePolicy
    (
        PolicyId        bigint IDENTITY(1,1) NOT NULL,
        PolicyKey       nvarchar(100) NOT NULL,          -- e.g. 'tool_catalog_context_pack', 'react_nudge'
        TenantId        nvarchar(50) NULL,
        ModuleKey       nvarchar(50) NULL,
        AppKey          nvarchar(50) NULL,
        Environment     nvarchar(50) NULL,               -- 'dev'/'staging'/'prod' (optional)
        Language        nvarchar(10) NULL,               -- optional
        Priority        int NOT NULL CONSTRAINT DF_RuntimePolicy_Priority DEFAULT (100),
        IsEnabled       bit NOT NULL CONSTRAINT DF_RuntimePolicy_IsEnabled DEFAULT (1),
        JsonValue       nvarchar(max) NOT NULL,          -- effective settings in JSON
        UpdatedAtUtc    datetime2(3) NOT NULL CONSTRAINT DF_RuntimePolicy_UpdatedAtUtc DEFAULT sysutcdatetime(),
        UpdatedBy       nvarchar(100) NULL,
        CONSTRAINT PK_RuntimePolicy PRIMARY KEY (PolicyId)
    );

    CREATE INDEX IX_RuntimePolicy_Lookup
        ON dbo.RuntimePolicy (PolicyKey, TenantId, ModuleKey, AppKey, Environment, Language, IsEnabled, Priority);
END
GO
