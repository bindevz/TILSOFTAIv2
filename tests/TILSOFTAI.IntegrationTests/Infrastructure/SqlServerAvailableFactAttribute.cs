using System;
using Xunit;

namespace TILSOFTAI.IntegrationTests.Infrastructure;

/// <summary>
/// Env-guarded external SQL validation attribute.
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
            Skip = "External SQL validation boundary not enabled. Set TEST_SQL_CONNECTION to run the deep integration suite.";
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
            Skip = "External SQL validation boundary not enabled. Set TEST_SQL_CONNECTION to run the deep integration suite.";
        }
    }
}
