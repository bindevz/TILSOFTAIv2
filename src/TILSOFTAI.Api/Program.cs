using TILSOFTAI.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Optional local override (gitignored) - loaded after defaults to allow secret overrides
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

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
