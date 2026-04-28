# Phase 1 — SQL Server 2025 Semantic Knowledge Base

## Goal

Move tool descriptions, multilingual aliases, examples, argument metadata, result schemas, answer policies, and semantic retrieval data into SQL Server 2025.

The production system must not depend on hardcoded C# descriptions for tool selection.

## Design principle

SQL Server 2025 is the semantic control plane.

C# loads metadata from SQL Server and builds candidate tools dynamically.

## Create schema

Create SQL scripts under:

```text
sql/ai/
  001_ai_schema.sql
  002_ai_capability_tables.sql
  003_ai_knowledge_tables.sql
  004_ai_entity_alias_tables.sql
  005_ai_execution_trace_tables.sql
  006_ai_indexes.sql
```

Base schema:

```sql
CREATE SCHEMA ai;
GO
```

## Capability table

```sql
CREATE TABLE ai.Capability
(
    CapabilityID        BIGINT IDENTITY PRIMARY KEY,
    CapabilityKey       NVARCHAR(200) NOT NULL UNIQUE,
    Domain              NVARCHAR(100) NOT NULL,
    BusinessArea        NVARCHAR(100) NULL,
    FunctionName        NVARCHAR(200) NOT NULL,
    AdapterType         NVARCHAR(50) NOT NULL,
    Operation           NVARCHAR(100) NOT NULL,
    StoredProcedure     SYSNAME NULL,
    ExecutionMode       NVARCHAR(50) NOT NULL,
    ArgumentContract    NVARCHAR(MAX) NULL CHECK (ArgumentContract IS NULL OR ISJSON(ArgumentContract) = 1),
    ResultSchema        NVARCHAR(MAX) NULL CHECK (ResultSchema IS NULL OR ISJSON(ResultSchema) = 1),
    AnswerPolicy        NVARCHAR(MAX) NULL CHECK (AnswerPolicy IS NULL OR ISJSON(AnswerPolicy) = 1),
    SensitivityPolicy   NVARCHAR(MAX) NULL CHECK (SensitivityPolicy IS NULL OR ISJSON(SensitivityPolicy) = 1),
    RequiredRoles       NVARCHAR(MAX) NULL CHECK (RequiredRoles IS NULL OR ISJSON(RequiredRoles) = 1),
    AllowedTenants      NVARCHAR(MAX) NULL CHECK (AllowedTenants IS NULL OR ISJSON(AllowedTenants) = 1),
    AllowMultiCall      BIT NOT NULL DEFAULT 0,
    IsActive            BIT NOT NULL DEFAULT 1,
    VersionNo           INT NOT NULL DEFAULT 1,
    UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

Use native `JSON` columns later if the current SQL Server 2025 deployment, driver, and tooling support them reliably. Start with `NVARCHAR(MAX) + ISJSON` for safer compatibility.

## Capability multilingual text

```sql
CREATE TABLE ai.CapabilityText
(
    CapabilityTextID BIGINT IDENTITY PRIMARY KEY,
    CapabilityKey    NVARCHAR(200) NOT NULL,
    Locale           NVARCHAR(20) NOT NULL,
    ShortName        NVARCHAR(300) NULL,
    Description      NVARCHAR(MAX) NOT NULL,
    UseWhen          NVARCHAR(MAX) NULL,
    DoNotUseWhen     NVARCHAR(MAX) NULL,
    BusinessNotes    NVARCHAR(MAX) NULL,
    UpdatedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_CapabilityText_Capability
        FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey)
);
GO
```

## Capability arguments

```sql
CREATE TABLE ai.CapabilityArgument
(
    ArgumentID          BIGINT IDENTITY PRIMARY KEY,
    CapabilityKey       NVARCHAR(200) NOT NULL,
    ArgumentName        NVARCHAR(128) NOT NULL,
    ProcParameterName   NVARCHAR(128) NOT NULL,
    DataType            NVARCHAR(50) NOT NULL,
    IsRequired          BIT NOT NULL,
    DefaultSource       NVARCHAR(100) NULL,
    ValidationRule      NVARCHAR(MAX) NULL CHECK (ValidationRule IS NULL OR ISJSON(ValidationRule) = 1),
    ClarificationPolicy NVARCHAR(MAX) NULL CHECK (ClarificationPolicy IS NULL OR ISJSON(ClarificationPolicy) = 1),
    DisplayOrder        INT NOT NULL DEFAULT 0,
    IsActive            BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_CapabilityArgument_Capability
        FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey)
);
GO
```

## Argument multilingual text

```sql
CREATE TABLE ai.ArgumentText
(
    ArgumentTextID BIGINT IDENTITY PRIMARY KEY,
    CapabilityKey  NVARCHAR(200) NOT NULL,
    ArgumentName   NVARCHAR(128) NOT NULL,
    Locale         NVARCHAR(20) NOT NULL,
    Description    NVARCHAR(MAX) NOT NULL,
    Aliases        NVARCHAR(MAX) NULL CHECK (Aliases IS NULL OR ISJSON(Aliases) = 1),
    Examples       NVARCHAR(MAX) NULL CHECK (Examples IS NULL OR ISJSON(Examples) = 1),
    UpdatedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

## Knowledge chunks for vector retrieval

```sql
CREATE TABLE ai.KnowledgeChunk
(
    ChunkID       BIGINT IDENTITY PRIMARY KEY,
    TenantID      NVARCHAR(100) NULL,
    ChunkType     NVARCHAR(50) NOT NULL,
    ObjectKey     NVARCHAR(300) NOT NULL,
    Domain        NVARCHAR(100) NULL,
    Locale        NVARCHAR(20) NULL,
    Title         NVARCHAR(300) NULL,
    ContentText   NVARCHAR(MAX) NOT NULL,
    Metadata      NVARCHAR(MAX) NULL CHECK (Metadata IS NULL OR ISJSON(Metadata) = 1),
    Embedding     VECTOR(1536) NULL,
    IsActive      BIT NOT NULL DEFAULT 1,
    UpdatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

`ChunkType` values:

```text
domain | capability | argument | result_field | glossary | example | entity_alias
```

## Entity aliases

```sql
CREATE TABLE ai.EntityAlias
(
    EntityAliasID     BIGINT IDENTITY PRIMARY KEY,
    TenantID          NVARCHAR(100) NULL,
    EntityType        NVARCHAR(100) NOT NULL,
    EntityID          NVARCHAR(100) NOT NULL,
    CanonicalCode     NVARCHAR(100) NULL,
    CanonicalName     NVARCHAR(300) NULL,
    AliasText         NVARCHAR(300) NOT NULL,
    AliasNormalized   NVARCHAR(300) NOT NULL,
    Locale            NVARCHAR(20) NULL,
    Metadata          NVARCHAR(MAX) NULL CHECK (Metadata IS NULL OR ISJSON(Metadata) = 1),
    Embedding         VECTOR(1536) NULL,
    IsActive          BIT NOT NULL DEFAULT 1,
    UpdatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

Entity types should include:

```text
customer | supplier | factory | warehouse | item | product_model | season | currency | country | employee | po | so | invoice | container
```

## Routing trace

```sql
CREATE TABLE ai.ToolRoutingTrace
(
    TraceID              BIGINT IDENTITY PRIMARY KEY,
    CorrelationID        UNIQUEIDENTIFIER NOT NULL,
    TenantID             NVARCHAR(100) NOT NULL,
    UserID               NVARCHAR(100) NOT NULL,
    Locale               NVARCHAR(20) NULL,
    UserMessageHash      VARBINARY(32) NULL,
    UserMessageRedacted  NVARCHAR(MAX) NULL,
    CandidateDomainsJson NVARCHAR(MAX) NULL CHECK (CandidateDomainsJson IS NULL OR ISJSON(CandidateDomainsJson) = 1),
    CandidateToolsJson   NVARCHAR(MAX) NULL CHECK (CandidateToolsJson IS NULL OR ISJSON(CandidateToolsJson) = 1),
    SelectedTool         NVARCHAR(200) NULL,
    ArgumentsJson        NVARCHAR(MAX) NULL CHECK (ArgumentsJson IS NULL OR ISJSON(ArgumentsJson) = 1),
    ValidationResultJson NVARCHAR(MAX) NULL CHECK (ValidationResultJson IS NULL OR ISJSON(ValidationResultJson) = 1),
    RowCount             INT NULL,
    AnswerMode           NVARCHAR(50) NULL,
    LatencyMs            INT NULL,
    Success              BIT NOT NULL,
    ErrorCode            NVARCHAR(100) NULL,
    CreatedAt            DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

## Repositories to add

Create under:

```text
src/TILSOFTAI.Infrastructure/SemanticSql/
```

Suggested interfaces/classes:

```csharp
public interface ISemanticKnowledgeRepository
{
    Task<IReadOnlyList<KnowledgeChunk>> SearchChunksAsync(
        SemanticSearchRequest request,
        CancellationToken cancellationToken);
}

public interface ICapabilityMetadataRepository
{
    Task<CapabilitySemanticMetadata> GetCapabilityMetadataAsync(
        string capabilityKey,
        string locale,
        CancellationToken cancellationToken);
}

public interface IEntityAliasRepository
{
    Task<IReadOnlyList<EntityCandidate>> SearchAliasesAsync(
        EntityAliasSearchRequest request,
        CancellationToken cancellationToken);
}

public interface IToolRoutingTraceStore
{
    Task SaveAsync(ToolRoutingTrace trace, CancellationToken cancellationToken);
}
```

## Seed data

Seed only 5 to 10 priority capabilities first:

```text
warehouse.inventory.by-item
warehouse.stock-movement.by-item
purchasing.po.open-by-supplier
sales.orders.by-customer
accounting.receivables.by-customer
product_model.by-code
```

For each seed capability, insert:

- `ai.Capability`
- `ai.CapabilityText` for `vi-VN` and `en-US`
- `ai.CapabilityArgument`
- `ai.ArgumentText` for `vi-VN` and `en-US`
- `ai.KnowledgeChunk` rows for capability, examples, arguments, and glossary terms
- relevant `ai.EntityAlias` examples

## Retrieval scoring

Implement a combined score:

```text
score =
  0.45 * vector_similarity
+ 0.20 * exact_alias_match
+ 0.15 * hard_signal_match
+ 0.10 * domain_prior
+ 0.10 * recent_success_trace_score
```

The exact formula can be adjusted, but retrieval must combine semantic, keyword, alias, and hard signal evidence.

## Acceptance criteria

- SQL scripts create the `ai` schema and core tables.
- Seed data exists for at least five priority capabilities.
- Repositories can load localized capability/argument metadata.
- Repositories can retrieve candidate domains/capabilities from user text.
- No production function description is hardcoded in C#.
- Vector support is optional during the first implementation but the schema must support it.
