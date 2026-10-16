var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(StockPilot.Api.Marker).Assembly);
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
app.Run();
public partial class Program { }
