using System;
using Xunit;

namespace TILSOFTAI.IntegrationTests.Infrastructure;

/// <summary>
/// PATCH 31.04: Env-guarded test attribute.
/// Test runs when TEST_SQL_CONNECTION is set, skips gracefully otherwise.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlServerAvailableFactAttribute : FactAttribute
{
    public SqlServerAvailableFactAttribute()
    {
        var conn = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            Skip = "SQL Server not available. Set TEST_SQL_CONNECTION environment variable.";
        }
    }
}

/// <summary>
/// Same as SqlServerAvailableFact but for Theory-based tests.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlServerAvailableTheoryAttribute : TheoryAttribute
{
    public SqlServerAvailableTheoryAttribute()
    {
        var conn = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            Skip = "SQL Server not available. Set TEST_SQL_CONNECTION environment variable.";
        }
    }
}
