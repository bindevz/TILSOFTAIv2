-- ============================================================
-- Patch 35.01: ReActFollowUpRule table
-- Explicit, scoped, versionable follow-up rules for ReAct depth.
-- LLM decides; C# only nudges based on these rules.
-- ============================================================

IF OBJECT_ID('dbo.ReActFollowUpRule', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReActFollowUpRule
    (
        RuleId              bigint IDENTITY(1,1) NOT NULL,
        RuleKey             nvarchar(120) NOT NULL,      -- stable identifier for audit
        TenantId            nvarchar(50) NULL,
        ModuleKey           nvarchar(50) NOT NULL,
        AppKey              nvarchar(50) NULL,
        ToolName            nvarchar(200) NULL,          -- trigger tool (optional)
        Priority            int NOT NULL CONSTRAINT DF_ReActFollowUpRule_Priority DEFAULT (100),
        IsEnabled           bit NOT NULL CONSTRAINT DF_ReActFollowUpRule_IsEnabled DEFAULT (1),

        -- Condition evaluation (kept intentionally simple & deterministic)
        JsonPath            nvarchar(300) NOT NULL,      -- e.g. '$.PieceCount'
        Operator            nvarchar(20)  NOT NULL,      -- 'exists' | '==' | '!=' | '>' | '>=' | '<' | '<=' | 'contains'
        CompareValue        nvarchar(200) NULL,          -- numeric or string (nullable when Operator='exists')

        -- Action for LLM (still LLM calls tools; C# only nudges)
        FollowUpToolName    nvarchar(200) NOT NULL,
        ArgsTemplateJson    nvarchar(max) NULL,          -- e.g. '{"modelId":"{{$.ModelId}}"}'
        PromptHint          nvarchar(800) NOT NULL,      -- injected/nudged instruction text
        UpdatedAtUtc        datetime2(3) NOT NULL CONSTRAINT DF_ReActFollowUpRule_UpdatedAtUtc DEFAULT sysutcdatetime(),
        UpdatedBy           nvarchar(100) NULL,

        CONSTRAINT PK_ReActFollowUpRule PRIMARY KEY (RuleId)
    );

    CREATE INDEX IX_ReActFollowUpRule_Scope
        ON dbo.ReActFollowUpRule (TenantId, ModuleKey, AppKey, ToolName, IsEnabled, Priority);
END
GO
