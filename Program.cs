using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Hubs;
using Unified.Middleware;
using Unified.Models.Identity;
using Unified.Services;

var builder = WebApplication.CreateBuilder(args);

// IIS / reverse-proxy forwarded headers (required for Cloudflare tunnel + IIS)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

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
builder.Services.AddHttpClient();
builder.Services.AddScoped<Unified.Services.TelegramService>();
builder.Services.AddHostedService<Unified.Services.CsRequestArchiveService>();
builder.Services.AddSingleton<IActivityLogQueue, ActivityLogQueueService>();
builder.Services.AddHostedService(sp => (ActivityLogQueueService)sp.GetRequiredService<IActivityLogQueue>());
builder.Services.AddHostedService<ActivityLogRetentionService>();
builder.Services.AddDataProtection();
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
        // Long-lived cache for versioned static assets
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
    }
});
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseRouting();
app.UseOutputCache();

app.UseAuthentication();
app.UseMiddleware<ActivityLoggingMiddleware>();
app.UseAuthorization();
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

