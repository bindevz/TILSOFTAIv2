using TILSOFTAI.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTilsoftAi(builder.Configuration);

var app = builder.Build();

app.MapTilsoftAi();

app.Run();

public partial class Program
{
}
