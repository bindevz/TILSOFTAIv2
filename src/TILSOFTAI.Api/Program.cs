using TILSOFTAI.Api.Extensions;
using TILSOFTAI.Domain.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Optional local override (gitignored) - loaded after defaults to allow secret overrides
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// PATCH 31.02: Validate no placeholder secrets at startup
var sqlConn = builder.Configuration["Sql:ConnectionString"];
if (string.IsNullOrEmpty(sqlConn) || sqlConn.Contains("__") || sqlConn.Contains("YOUR_"))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Sql:ConnectionString is not configured. " +
            "Set via environment variable Sql__ConnectionString or secret store.");
    }
    else
    {
        // Dev mode: warn but don't crash (user secrets may load later)
        Console.WriteLine("WARNING: Sql:ConnectionString appears to be a placeholder. " +
            "Use 'dotnet user-secrets set \"Sql:ConnectionString\" \"...\"' to configure.");
    }
}

// Configure Kestrel server limits for request body size enforcement
// Reading configuration directly here is acceptable as this is host builder time, not runtime
builder.WebHost.ConfigureKestrel((context, options) =>
{
    var maxRequestBytes = context.Configuration.GetValue<long?>("Chat:MaxRequestBytes") ?? 1048576; // 1MB default
    
    options.Limits.MaxRequestBodySize = maxRequestBytes;
});

builder.Services.AddTilsoftAi(builder.Configuration);

var app = builder.Build();


// HTTPS redirection and HSTS in production only
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.MapTilsoftAi();

app.Run();
