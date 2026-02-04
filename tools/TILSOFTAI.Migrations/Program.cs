using System.Reflection;
using DbUp;
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

if (command == "migrate")
{
    return ExecuteMigrate(connection, environment, dryRun, verbose);
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

static int ExecuteMigrate(string? connection, string environment, bool dryRun, bool verbose)
{
    var connString = GetConnectionString(connection, environment);
    if (string.IsNullOrEmpty(connString))
    {
        Console.Error.WriteLine("Connection string not found.");
        Console.Error.WriteLine("Provide via --connection option, appsettings.json, or TILSOFTAI_CONNECTION_STRING environment variable.");
        return 1;
    }

    Console.WriteLine($"Migrating database ({environment})...");
    
    var upgrader = DeployChanges.To
        .SqlDatabase(connString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .WithTransactionPerScript()
        .JournalToSqlTable("dbo", "SchemaVersions")
        .LogToConsole()
        .Build();

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

    var result = upgrader.PerformUpgrade();

    if (!result.Successful)
    {
        Console.Error.WriteLine($"Migration failed: {result.Error}");
        return 1;
    }

    Console.WriteLine("Migration completed successfully.");
    return 0;
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

    var upgrader = DeployChanges.To
        .SqlDatabase(connString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .JournalToSqlTable("dbo", "SchemaVersions")
        .Build();

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
    Console.WriteLine("EXAMPLES:");
    Console.WriteLine("  dotnet run -- status --environment Development");
    Console.WriteLine("  dotnet run -- migrate --dry-run");
    Console.WriteLine("  dotnet run -- migrate --connection \"Server=...;Database=TILSOFTAI;...\"");
}
