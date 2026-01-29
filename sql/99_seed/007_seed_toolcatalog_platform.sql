SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- diagnostics_run
IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'diagnostics_run')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (
        ToolName,
        SpName,
        IsEnabled,
        RequiredRoles,
        JsonSchema,
        Instruction,
        Description
    )
    VALUES
    (
        'diagnostics_run',
        'ai_diagnostics_run',
        1,
        NULL,
        '{"type":"object","required":["module","ruleKey"],"properties":{"module":{"type":"string"},"ruleKey":{"type":"string"},"inputJson":{"type":["string","null"]}},"additionalProperties":false}',
        'Run a diagnostics rule to validate data or configuration. Provide module name, ruleKey, and optional inputJson. Returns validation results.',
        'Execute diagnostics rule and return validation results.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'diagnostics_run' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'diagnostics_run',
        'en',
        'Run a diagnostics rule to validate data or configuration. Provide module name, ruleKey, and optional inputJson. Returns validation results.',
        'Execute diagnostics rule and return validation results.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'diagnostics_run' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'diagnostics_run',
        'vi',
        'Chay quy tac diagnostics de kiem tra du lieu hoac cau hinh. Cung cap ten module, ruleKey, va inputJson tuy chon. Tra ve ket qua kiem tra.',
        'Thuc thi quy tac diagnostics va tra ve ket qua kiem tra.'
    );
END;
GO

-- action_request_write
IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'action_request_write')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (
        ToolName,
        SpName,
        IsEnabled,
        RequiredRoles,
        JsonSchema,
        Instruction,
        Description
    )
    VALUES
    (
        'action_request_write',
        'ai_action_request_write',
        1,
        NULL,
        '{"type":"object","required":["proposedToolName","proposedSpName","argsJson"],"properties":{"proposedToolName":{"type":"string"},"proposedSpName":{"type":"string"},"argsJson":{"type":"string"}},"additionalProperties":false}',
        'Request a write action for human approval. Provide proposedToolName, proposedSpName, and argsJson. This does NOT execute the action immediately - it creates a pending request for approval. Returns actionId and status.',
        'Request a write action for human approval (does not execute immediately).'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'action_request_write' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'action_request_write',
        'en',
        'Request a write action for human approval. Provide proposedToolName, proposedSpName, and argsJson. This does NOT execute the action immediately - it creates a pending request for approval. Returns actionId and status.',
        'Request a write action for human approval (does not execute immediately).'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'action_request_write' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'action_request_write',
        'vi',
        'Yeu cau thao tac ghi de phe duyet cua con nguoi. Cung cap proposedToolName, proposedSpName, va argsJson. Khong thuc thi ngay - tao yeu cau cho doi phe duyet. Tra ve actionId va trang thai.',
        'Yeu cau thao tac ghi de phe duyet cua con nguoi (khong thuc thi ngay).'
    );
END;
GO
