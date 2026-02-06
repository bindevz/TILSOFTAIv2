using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Configuration;

if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
{
    ShowHelp();
    return 0;
}

var command = args[0].ToLowerInvariant();
var connection = GetArgValue(args, "--connection");
var environment = GetArgValue(args, "--environment") ?? "Development";
var dryRun = HasFlag(args, "--dry-run");
var verbose = HasFlag(args, "--verbose");
var pendingOnly = HasFlag(args, "--pending-only");
var autoRetry = HasFlag(args, "--auto-retry");
var maxRetries = int.TryParse(GetArgValue(args, "--max-retries"), out var r) ? r : 3;
var retryDelaySeconds = int.TryParse(GetArgValue(args, "--retry-delay"), out var d) ? d : 30;

if (command == "migrate")
{
    return ExecuteMigrate(connection, environment, dryRun, verbose, autoRetry, maxRetries, retryDelaySeconds);
}
else if (command == "status")
{
    return ExecuteStatus(connection, environment, pendingOnly);
}
else
{
    Console.Error.WriteLine($"Unknown command: {command}");
    ShowHelp();
    return 1;
}

/// <summary>
/// PATCH 32.01: Build upgrader with filesystem scripts instead of embedded assembly.
/// </summary>
static UpgradeEngine BuildUpgrader(string connectionString, string sqlPath)
{
    var options = new DbUp.ScriptProviders.FileSystemScriptOptions
    {
        IncludeSubDirectories = true
    };
    
    // Ensure the journal table exists before running any scripts
    EnsureDatabase.For.SqlDatabase(connectionString);
    
    return DeployChanges.To
        .SqlDatabase(connectionString)
        .WithScriptsFromFileSystem(sqlPath, options)
        .WithTransactionPerScript()
        .JournalToSqlTable("dbo", "SchemaVersions")
        .LogToConsole()
        .Build();
}

/// <summary>
/// PATCH 32.01: Get path to SQL scripts folder (relative to execution folder).
/// </summary>
static string GetSqlScriptsPath()
{
    // Relative to execution folder: ../../sql
    var basePath = AppContext.BaseDirectory;
    var sqlPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "..", "sql"));
    
    // Fallback: check if running from repo root
    if (!Directory.Exists(sqlPath))
    {
        sqlPath = Path.GetFullPath(Path.Combine(basePath, "sql"));
    }
    
    // Another fallback: current directory
    if (!Directory.Exists(sqlPath))
    {
        sqlPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "sql"));
    }
    
    return sqlPath;
}

static int ExecuteMigrate(string? connection, string environment, bool dryRun, bool verbose, 
    bool autoRetry, int maxRetries, int retryDelaySeconds)
{
    var connString = GetConnectionString(connection, environment);
    if (string.IsNullOrEmpty(connString))
    {
        Console.Error.WriteLine("Connection string not found.");
        Console.Error.WriteLine("Provide via --connection option, appsettings.json, or TILSOFTAI_CONNECTION_STRING environment variable.");
        return 1;
    }

    var sqlPath = GetSqlScriptsPath();
    if (!Directory.Exists(sqlPath))
    {
        Console.Error.WriteLine($"SQL scripts folder not found: {sqlPath}");
        return 1;
    }

    Console.WriteLine($"Migrating database ({environment})...");
    Console.WriteLine($"SQL scripts path: {sqlPath}");
    
    var upgrader = BuildUpgrader(connString, sqlPath);

    if (dryRun)
    {
        var scripts = upgrader.GetScriptsToExecute();
        Console.WriteLine($"Pending migrations ({scripts.Count}):");
        foreach (var script in scripts)
        {
            Console.WriteLine($"  - {script.Name}");
        }
        return 0;
    }

    // PATCH 32.01: Retry loop for self-healing workflow
    var attempt = 0;
    while (true)
    {
        attempt++;
        
        // Rebuild upgrader to pick up any file changes
        upgrader = BuildUpgrader(connString, sqlPath);
        var result = upgrader.PerformUpgrade();

        if (result.Successful)
        {
            Console.WriteLine("Migration completed successfully.");
            return 0;
        }

        // PATCH 32.01: Structured error logging for automation
        LogMigrationError(result);

        if (!autoRetry || attempt >= maxRetries)
        {
            Console.Error.WriteLine($"Migration failed after {attempt} attempt(s).");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine($"[RETRY_PENDING] Attempt {attempt}/{maxRetries} failed.");
        Console.WriteLine($"[RETRY_PENDING] Waiting {retryDelaySeconds} seconds for external fix...");
        Console.WriteLine($"[RETRY_PENDING] An agent or operator can modify the SQL file at: {sqlPath}");
        Console.WriteLine();
        
        Thread.Sleep(retryDelaySeconds * 1000);
        Console.WriteLine("[RETRY] Retrying migration...");
    }
}

/// <summary>
/// PATCH 32.01: Log migration error in machine-parseable format for automation.
/// </summary>
static void LogMigrationError(DatabaseUpgradeResult result)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("[SQL_ERROR_DETECTED]");
    Console.Error.WriteLine($"File: {result.ErrorScript?.Name ?? "Unknown"}");
    Console.Error.WriteLine($"Error: {result.Error?.Message ?? "Unknown error"}");
    
    // Try to extract SQL context from the error
    var sqlContext = ExtractSqlContext(result.Error);
    if (!string.IsNullOrEmpty(sqlContext))
    {
        Console.Error.WriteLine($"Context: {sqlContext}");
    }
    
    // Full exception details
    if (result.Error != null)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("[FULL_EXCEPTION]");
        Console.Error.WriteLine(result.Error.ToString());
    }
    Console.Error.WriteLine();
}

/// <summary>
/// PATCH 32.01: Extract SQL context from exception if available.
/// </summary>
static string? ExtractSqlContext(Exception? ex)
{
    if (ex == null) return null;
    
    // Try to get inner exception message which often contains SQL details
    var innerMessage = ex.InnerException?.Message;
    if (!string.IsNullOrEmpty(innerMessage) && innerMessage.Length < 500)
    {
        return innerMessage;
    }
    
    // Truncate main message if it contains SQL
    var msg = ex.Message;
    if (msg.Length > 200)
    {
        return msg.Substring(0, 200) + "...";
    }
    
    return msg;
}

static int ExecuteStatus(string? connection, string environment, bool pendingOnly)
{
    var connString = GetConnectionString(connection, environment);
    if (string.IsNullOrEmpty(connString))
    {
        Console.Error.WriteLine("Connection string not found.");
        Console.Error.WriteLine("Provide via --connection option, appsettings.json, or TILSOFTAI_CONNECTION_STRING environment variable.");
        return 1;
    }

    var sqlPath = GetSqlScriptsPath();
    if (!Directory.Exists(sqlPath))
    {
        Console.Error.WriteLine($"SQL scripts folder not found: {sqlPath}");
        return 1;
    }

    Console.WriteLine($"SQL scripts path: {sqlPath}");
    
    var upgrader = BuildUpgrader(connString, sqlPath);

    var executed = upgrader.GetExecutedScripts();
    var pending = upgrader.GetScriptsToExecute();

    if (!pendingOnly)
    {
        Console.WriteLine($"Executed migrations ({executed.Count}):");
        foreach (var script in executed)
        {
            Console.WriteLine($"  ✓ {script}");
        }
    }

    Console.WriteLine($"Pending migrations ({pending.Count}):");
    foreach (var script in pending)
    {
        Console.WriteLine($"  ○ {script.Name}");
    }
    
    return 0;
}

static string? GetConnectionString(string? explicitConnection, string environment)
{
    if (!string.IsNullOrEmpty(explicitConnection))
        return explicitConnection;

    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{environment}.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    return config["Sql:ConnectionString"] 
        ?? Environment.GetEnvironmentVariable("TILSOFTAI_CONNECTION_STRING");
}

static string? GetArgValue(string[] args, string flag)
{
    var index = Array.IndexOf(args, flag);
    if (index >= 0 && index + 1 < args.Length)
        return args[index + 1];
    return null;
}

static bool HasFlag(string[] args, string flag)
{
    return Array.IndexOf(args, flag) >= 0;
}

static void ShowHelp()
{
    Console.WriteLine("TILSOFTAI Database Migration Tool");
    Console.WriteLine();
    Console.WriteLine("USAGE:");
    Console.WriteLine("  dotnet run -- <command> [options]");
    Console.WriteLine();
    Console.WriteLine("COMMANDS:");
    Console.WriteLine("  migrate         Apply pending migrations");
    Console.WriteLine("  status          Show migration status");
    Console.WriteLine();
    Console.WriteLine("OPTIONS:");
    Console.WriteLine("  --connection <string>     SQL Server connection string");
    Console.WriteLine("  --environment <string>    Target environment (Development, Staging, Production)");
    Console.WriteLine("  --dry-run                 Show what would be executed without running (migrate only)");
    Console.WriteLine("  --verbose                 Enable verbose output");
    Console.WriteLine("  --pending-only            Show only pending migrations (status only)");
    Console.WriteLine();
    Console.WriteLine("SELF-HEALING OPTIONS (PATCH 32.01):");
    Console.WriteLine("  --auto-retry              Enable automatic retry after failure (for agent-driven fixes)");
    Console.WriteLine("  --max-retries <n>         Maximum retry attempts (default: 3)");
    Console.WriteLine("  --retry-delay <seconds>   Delay between retries (default: 30)");
    Console.WriteLine();
    Console.WriteLine("EXAMPLES:");
    Console.WriteLine("  dotnet run -- status --environment Development");
    Console.WriteLine("  dotnet run -- migrate --dry-run");
    Console.WriteLine("  dotnet run -- migrate --connection \"Server=...;Database=TILSOFTAI;...\"");
    Console.WriteLine("  dotnet run -- migrate --auto-retry --max-retries 5 --retry-delay 60");
}
