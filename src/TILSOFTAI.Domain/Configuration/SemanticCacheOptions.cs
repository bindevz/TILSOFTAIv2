namespace TILSOFTAI.Domain.Configuration;

public sealed class SemanticCacheOptions
{
    public bool Enabled { get; set; } = true;
    public bool AllowSensitiveContent { get; set; }
    public bool EnableSqlVectorCache { get; set; }
    public string Mode { get; set; } = "RedisHash";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    
    /// <summary>
    /// Use SQL Server 2025 AI_GENERATE_EMBEDDINGS instead of external embedding API.
    /// Requires SQL Server 2025 with EXTERNAL MODEL configured.
    /// Falls back to C# embeddings if function is unavailable.
    /// </summary>
    public bool UseSqlEmbeddings { get; set; } = false;
    
    /// <summary>
    /// Model name for SQL AI_GENERATE_EMBEDDINGS function.
    /// Must match an existing EXTERNAL MODEL in SQL Server.
    /// </summary>
    public string SqlEmbeddingModelName { get; set; } = "text-embedding-3-small";
}
