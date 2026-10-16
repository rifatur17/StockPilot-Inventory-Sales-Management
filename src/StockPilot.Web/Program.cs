using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StockPilot.Core;
using StockPilot.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
// Test hosts must resolve build-time compressed assets, as Development already does.
if (builder.Environment.IsEnvironment("Testing")) builder.WebHost.UseStaticWebAssets();
var cs = builder.Configuration.GetConnectionString("StockPilot");
if (!builder.Environment.IsEnvironment("Testing") && string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Set ConnectionStrings__StockPilot using environment variables or User Secrets.");
builder.Services.AddDbContext<AppDb>(o => o.UseSqlServer(cs ?? "Server=unconfigured.invalid;Database=not_used;Integrated Security=true"));
builder.Services.AddScoped<BusinessService>(); builder.Services.AddScoped<LedgerGate>(); builder.Services.AddScoped<UserService>();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 12; o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<AppDb>().AddSignInManager<ActiveSignInManager>().AddDefaultTokenProviders();
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/Denied";
    o.Cookie.Name = "StockPilot.Auth"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => CookieResponse(c, 401);
    o.Events.OnRedirectToAccessDenied = c => CookieResponse(c, 403);
});
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.HttpOnly = true; });
builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute())).AddApplicationPart(typeof(StockPilot.Api.Marker).Assembly).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi(); builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
});
var app = builder.Build();
if (args.Contains("--bootstrap-admin"))
{
    using var scope = app.Services.CreateScope();
    var email = builder.Configuration["Bootstrap:Email"] ?? throw new InvalidOperationException("Set Bootstrap__Email.");
    var password = builder.Configuration["Bootstrap:Password"] ?? throw new InvalidOperationException("Set Bootstrap__Password securely.");
    await scope.ServiceProvider.GetRequiredService<UserService>().Bootstrap(email, password); return;
}
app.UseExceptionHandler(e => e.Run(async c =>
{
    var error = c.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var status = error is BusinessException b ? b.Status : 500;
    c.Response.StatusCode = status;
    var message = error is BusinessException business ? business.Message : "The operation could not be completed. Contact an administrator with the request identifier.";
    if (c.Request.Path.StartsWithSegments("/api")) await Results.Problem(title: message, statusCode: status, extensions: new Dictionary<string, object?> { ["traceId"] = c.TraceIdentifier }).ExecuteAsync(c);
    else { c.Response.ContentType = "text/html"; await c.Response.WriteAsync($"<!doctype html><html lang='en'><title>StockPilot · Request issue</title><body><h1>Request could not be completed ({status})</h1><p>{System.Net.WebUtility.HtmlEncode(message)}</p><p>Use your browser Back button to return to the form.</p><a href='/'>Dashboard</a></body></html>"); }
}));
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) { app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (c, next) =>
{
    c.Response.Headers["X-Content-Type-Options"] = "nosniff"; c.Response.Headers["Referrer-Policy"] = "same-origin";
    c.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (!c.Request.Path.StartsWithSegments("/assets")) c.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStatusCodePages(async s =>
{
    if (s.HttpContext.Request.Path.StartsWithSegments("/api")) await Results.Problem(statusCode: s.HttpContext.Response.StatusCode).ExecuteAsync(s.HttpContext);
    else { s.HttpContext.Response.ContentType = "text/html"; await s.HttpContext.Response.WriteAsync("<!doctype html><html lang='en'><title>StockPilot</title><h1>Page unavailable</h1><a href='/'>Return to dashboard</a></html>"); }
});
app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
app.MapStaticAssets().AllowAnonymous(); app.MapControllers(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapGet("/health", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.Run();
static Task CookieResponse(RedirectContext<CookieAuthenticationOptions> c, int status)
{
    if (c.Request.Path.StartsWithSegments("/api")) return Results.Problem(statusCode: status).ExecuteAsync(c.HttpContext);
    c.Response.Redirect(c.RedirectUri); return Task.CompletedTask;
}
public partial class Program { }
