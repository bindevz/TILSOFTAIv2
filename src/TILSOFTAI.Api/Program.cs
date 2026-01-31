using TILSOFTAI.Api.Extensions;
using TILSOFTAI.Domain.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Optional local override (gitignored) - loaded after defaults to allow secret overrides
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Configure Kestrel server limits for request body size enforcement
builder.WebHost.ConfigureKestrel((context, options) =>
{
    var chatOptions = context.Configuration
        .GetSection(ConfigurationSectionNames.Chat)
        .Get<ChatOptions>() ?? new ChatOptions();
    
    options.Limits.MaxRequestBodySize = chatOptions.MaxRequestBytes;
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
