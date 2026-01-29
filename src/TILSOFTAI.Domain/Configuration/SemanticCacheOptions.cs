namespace TILSOFTAI.Domain.Configuration;

public sealed class SemanticCacheOptions
{
    public bool Enabled { get; set; } = true;
    public bool AllowSensitiveContent { get; set; }
    public bool EnableSqlVectorCache { get; set; }
    public string Mode { get; set; } = "RedisHash";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}
