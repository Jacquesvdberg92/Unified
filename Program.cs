using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Hubs;
using Unified.Middleware;
using Unified.Models.Identity;
using Unified.Services;

var builder = WebApplication.CreateBuilder(args);

// IIS / reverse-proxy forwarded headers (required for Cloudflare tunnel + IIS).
// Only proxies on this machine (loopback, the default) are trusted, so clients can't spoof
// their IP or scheme. A proxy elsewhere (e.g. cloudflared on another host) must be listed
// under ForwardedHeaders:KnownProxies / KnownNetworks.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));

    // Escape hatch for setups where the proxy address isn't known up front. Lets any client
    // spoof X-Forwarded-For, so only use it when the app is reachable solely through the proxy.
    if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustAllProxies"))
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<Unified.Hubs.HubConnectionTracker>();

// Allow large file uploads (30 MB) for brand document uploads
builder.WebHost.ConfigureKestrel(opts =>
    opts.Limits.MaxRequestBodySize = 31_457_280); // 30 MB

// Response compression (Brotli preferred, Gzip fallback)
// EnableForHttps is left false in development to avoid content-encoding errors;
// in production a reverse proxy (IIS/nginx) handles HTTPS compression safely.
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = !builder.Environment.IsDevelopment();
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "text/html", "text/css", "application/javascript" });
});

// Output cache for read-heavy pages (reports, leaderboard)
builder.Services.AddOutputCache(opts =>
{
    opts.AddPolicy("Reports",  p => p.Expire(TimeSpan.FromMinutes(5)));
    opts.AddPolicy("LeaderBoard", p => p.Expire(TimeSpan.FromMinutes(5)));
});

// Memory cache for frequently read reference data
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath  = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
});

// Internal-only policy – every role except AccountManager (external users).
// Controllers that are AM-accessible must explicitly allow AccountManager.
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("InternalOnly", p => p.RequireRole(
        Roles.BrandManager, Roles.TeamLeader, Roles.CSAgent,
        Roles.SwissArmyKnife, Roles.Finance));
});

builder.Services.AddScoped<Unified.Services.EmailTemplateService>();
builder.Services.AddScoped<Unified.Services.ProcessTemplateService>();
builder.Services.AddScoped<Unified.Services.UpdateService>();
builder.Services.AddScoped<Unified.Services.ScheduleService>();
builder.Services.AddScoped<Unified.Services.PerformanceService>();
builder.Services.AddScoped<Unified.Services.VaultService>();
builder.Services.AddScoped<Unified.Services.ReportService>();
builder.Services.AddScoped<Unified.Services.AttendanceService>();
builder.Services.AddScoped<Unified.Services.WorkDistributionService>();
builder.Services.AddScoped<Unified.Services.CsLiveAllocationService>();
builder.Services.AddScoped<Unified.Services.PoiSimulationService>();
builder.Services.AddScoped<Unified.Services.DashboardService>();
builder.Services.AddScoped<Unified.Services.ReferenceDataService>();
builder.Services.AddScoped<Unified.Services.CsLiveHelpService>();
builder.Services.AddScoped<Unified.Services.CsMessagingService>();
builder.Services.AddScoped<Unified.Services.WithdrawalService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient(AiContentService.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<Unified.Services.AiContentService>();
builder.Services.AddHttpClient(GitHubIssueService.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<Unified.Services.GitHubIssueService>();
builder.Services.AddScoped<Unified.Services.BugReportService>();
builder.Services.AddHostedService<Unified.Services.BugReportRetryService>();

// Per-user cap on AI rewrites so a single account can't run up the OpenRouter bill
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.AddPolicy(AiContentService.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(10) }));
    opts.OnRejected = (ctx, ct) => new ValueTask(ctx.HttpContext.Response.WriteAsJsonAsync(
        new { message = "You've reached the AI limit for now. Please try again in a few minutes." }, ct));
});
builder.Services.AddScoped<Unified.Services.TelegramService>();
builder.Services.AddHostedService<Unified.Services.CsRequestArchiveService>();
builder.Services.AddSingleton<IActivityLogQueue, ActivityLogQueueService>();
builder.Services.AddHostedService(sp => (ActivityLogQueueService)sp.GetRequiredService<IActivityLogQueue>());
builder.Services.AddHostedService<ActivityLogRetentionService>();
// Vault passwords and login cookies are encrypted with this key ring; losing it makes them
// unreadable. Set DataProtection:KeysPath to a backed-up folder to control where it lives
// (unset keeps the platform default location, so existing keys keep working).
var dataProtection = builder.Services.AddDataProtection();
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var request = ctx.Context.Request;
        string cacheControl;
        if (request.Query.ContainsKey("v"))
            cacheControl = "public, max-age=31536000, immutable";   // asp-append-version: URL changes with content
        else if (request.Path.StartsWithSegments("/uploads"))
            cacheControl = "private, max-age=86400";                 // user files: don't keep in shared caches
        else if (request.Path.StartsWithSegments("/assets") || request.Path.StartsWithSegments("/lib"))
            cacheControl = "public, max-age=604800";                 // vendor theme / libraries
        else
            cacheControl = "no-cache";                               // revalidate so deploys show up immediately
        ctx.Context.Response.Headers.CacheControl = cacheControl;
    }
});
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseRouting();
app.UseOutputCache();

app.UseAuthentication();
app.UseMiddleware<ActivityLoggingMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseSession();

// Map controllers with attribute routing (for API)
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHub<CsLiveHelpHub>("/hubs/cslivehelp");
app.MapHub<CsMessagingHub>("/hubs/cs-messaging");

// Seed database
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitialiseAsync(scope.ServiceProvider);

    if (app.Configuration["Seed:LoadDemoData"] == "true")
        await DemoSeedData.LoadAsync(scope.ServiceProvider);
}

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }

