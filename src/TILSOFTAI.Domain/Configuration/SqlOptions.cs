using Microsoft.Data.SqlClient;

namespace TILSOFTAI.Domain.Configuration;

public sealed class SqlOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = ConfigurationDefaults.Sql.CommandTimeoutSeconds;
    
    /// <summary>
    /// Minimum number of connections maintained in the pool.
    /// </summary>
    public int MinPoolSize { get; set; } = ConfigurationDefaults.Sql.MinPoolSize;

    /// <summary>
    /// Maximum number of connections allowed in the pool.
    /// </summary>
    public int MaxPoolSize { get; set; } = ConfigurationDefaults.Sql.MaxPoolSize;

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = ConfigurationDefaults.Sql.ConnectionTimeoutSeconds;

    /// <summary>
    /// Application name for SQL Server connection identification.
    /// </summary>
    public string ApplicationName { get; set; } = ConfigurationDefaults.Sql.ApplicationName;

    /// <summary>
    /// Builds connection string with pooling parameters.
    /// </summary>
    public string GetPooledConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString)
        {
            MinPoolSize = MinPoolSize,
            MaxPoolSize = MaxPoolSize,
            ConnectTimeout = ConnectionTimeoutSeconds,
            ApplicationName = ApplicationName,
            Pooling = true
        };
        return builder.ConnectionString;
    }
}
