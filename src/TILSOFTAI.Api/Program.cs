using TILSOFTAI.Api.Extensions;
using TILSOFTAI.Domain.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Optional local override (gitignored) - loaded after defaults to allow secret overrides
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

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

public partial class Program
{
}
