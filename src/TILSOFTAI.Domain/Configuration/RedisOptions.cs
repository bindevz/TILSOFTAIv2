namespace TILSOFTAI.Domain.Configuration;

public sealed class RedisOptions
{
    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public int DefaultTtlMinutes { get; set; } = ConfigurationDefaults.Redis.DefaultTtlMinutes;
}
