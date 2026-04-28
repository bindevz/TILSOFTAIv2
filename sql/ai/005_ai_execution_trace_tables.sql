IF OBJECT_ID('ai.ToolRoutingTrace', 'U') IS NULL
BEGIN
    CREATE TABLE ai.ToolRoutingTrace
    (
        TraceID BIGINT IDENTITY CONSTRAINT PK_ai_ToolRoutingTrace PRIMARY KEY,
        CorrelationID UNIQUEIDENTIFIER NOT NULL,
        TenantID NVARCHAR(100) NOT NULL,
        UserID NVARCHAR(100) NOT NULL,
        Locale NVARCHAR(20) NULL,
        UserMessageHash VARBINARY(32) NULL,
        UserMessageRedacted NVARCHAR(MAX) NULL,
        CandidateDomainsJson NVARCHAR(MAX) NULL,
        CandidateToolsJson NVARCHAR(MAX) NULL,
        HardSignalsJson NVARCHAR(MAX) NULL,
        AdvertisedFunctionToolsJson NVARCHAR(MAX) NULL,
        SelectedTool NVARCHAR(200) NULL,
        SelectedFunction NVARCHAR(200) NULL,
        CandidateDomainCount INT NULL,
        CandidateCapabilityCount INT NULL,
        AdvertisedToolCount INT NULL,
        ArgumentsJson NVARCHAR(MAX) NULL,
        ArgumentsBeforeNormalizationJson NVARCHAR(MAX) NULL,
        ArgumentsAfterNormalizationJson NVARCHAR(MAX) NULL,
        ValidationResultJson NVARCHAR(MAX) NULL,
        AdapterType NVARCHAR(100) NULL,
        RowCount INT NULL,
        AnswerMode NVARCHAR(50) NULL,
        LatencyMs INT NULL,
        LatencyByStageJson NVARCHAR(MAX) NULL,
        ModelProvider NVARCHAR(100) NULL,
        Success BIT NOT NULL,
        ErrorCode NVARCHAR(100) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ai_ToolRoutingTrace_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_ai_ToolRoutingTrace_CandidateDomainsJson CHECK (CandidateDomainsJson IS NULL OR ISJSON(CandidateDomainsJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_CandidateToolsJson CHECK (CandidateToolsJson IS NULL OR ISJSON(CandidateToolsJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_HardSignalsJson CHECK (HardSignalsJson IS NULL OR ISJSON(HardSignalsJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_AdvertisedFunctionToolsJson CHECK (AdvertisedFunctionToolsJson IS NULL OR ISJSON(AdvertisedFunctionToolsJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_ArgumentsJson CHECK (ArgumentsJson IS NULL OR ISJSON(ArgumentsJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_ArgumentsBeforeNormalizationJson CHECK (ArgumentsBeforeNormalizationJson IS NULL OR ISJSON(ArgumentsBeforeNormalizationJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_ArgumentsAfterNormalizationJson CHECK (ArgumentsAfterNormalizationJson IS NULL OR ISJSON(ArgumentsAfterNormalizationJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_ValidationResultJson CHECK (ValidationResultJson IS NULL OR ISJSON(ValidationResultJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_LatencyByStageJson CHECK (LatencyByStageJson IS NULL OR ISJSON(LatencyByStageJson) = 1)
    );
END;
GO

IF OBJECT_ID('ai.ToolRoutingTrace', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('ai.ToolRoutingTrace', 'HardSignalsJson') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD HardSignalsJson NVARCHAR(MAX) NULL;
    IF COL_LENGTH('ai.ToolRoutingTrace', 'AdvertisedFunctionToolsJson') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD AdvertisedFunctionToolsJson NVARCHAR(MAX) NULL;
    IF COL_LENGTH('ai.ToolRoutingTrace', 'ArgumentsBeforeNormalizationJson') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD ArgumentsBeforeNormalizationJson NVARCHAR(MAX) NULL;
    IF COL_LENGTH('ai.ToolRoutingTrace', 'ArgumentsAfterNormalizationJson') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD ArgumentsAfterNormalizationJson NVARCHAR(MAX) NULL;
    IF COL_LENGTH('ai.ToolRoutingTrace', 'AdapterType') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD AdapterType NVARCHAR(100) NULL;
    IF COL_LENGTH('ai.ToolRoutingTrace', 'LatencyByStageJson') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD LatencyByStageJson NVARCHAR(MAX) NULL;
    IF COL_LENGTH('ai.ToolRoutingTrace', 'ModelProvider') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD ModelProvider NVARCHAR(100) NULL;
    IF COL_LENGTH('ai.ToolRoutingTrace', 'SelectedFunction') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD SelectedFunction NVARCHAR(200) NULL;
    IF COL_LENGTH('ai.ToolRoutingTrace', 'CandidateDomainCount') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD CandidateDomainCount INT NULL;
    IF COL_LENGTH('ai.ToolRoutingTrace', 'CandidateCapabilityCount') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD CandidateCapabilityCount INT NULL;
    IF COL_LENGTH('ai.ToolRoutingTrace', 'AdvertisedToolCount') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD AdvertisedToolCount INT NULL;
END;
GO
