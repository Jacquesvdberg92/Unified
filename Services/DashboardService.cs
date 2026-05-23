using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Dashboard;

namespace Unified.Services;

public record WidgetDefinition(string Key, string Title, string Icon, string Description);

public class DashboardService
{
    private readonly AppDbContext _db;

    public static readonly IReadOnlyList<WidgetDefinition> Catalog = new List<WidgetDefinition>
    {
        new("updates_feed",        "Updates Feed",          "bx bx-news",              "Latest pinned and recent updates"),
        new("work_distribution",   "Work Distribution",     "bx bx-list-ul",           "Today's work distribution roster"),
        new("poi_simulations",     "POI Simulations",       "bx bx-id-card",           "Recent POI simulation entries"),
        new("poi_report",          "POI Report",            "bx bx-file-find",         "POI simulation pass/fail summary"),
        new("my_schedule",         "My Schedule",           "bx bx-calendar-check",    "Your upcoming shift schedule"),
        new("performance",         "My Performance",        "bx bx-bar-chart-alt-2",   "Latest performance review summary"),
        new("attendance",          "Attendance",            "bx bx-time-five",         "Your attendance log summary"),
        new("reports",             "Reports",               "bx bx-line-chart",        "Team report overview"),
        new("cs_live_help",        "CS Live Help",          "bx bx-headphone",         "CS Live Help slot overview"),
        new("quick_links",         "Quick Links",           "bx bx-link",              "Handy shortcuts to key pages"),
        new("request_login",       "Request Login",         "bx bx-desktop",           "Send your AnyDesk ID to IT via Telegram to request a Google login"),
    };

    public DashboardService(AppDbContext db) => _db = db;

    public async Task<List<DashboardWidget>> GetUserWidgetsAsync(string userId)
    {
        return await _db.DashboardWidgets
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.SortOrder)
            .ToListAsync();
    }

    public async Task SaveUserWidgetsAsync(string userId, List<(string key, int colSpan)> widgets)
    {
        var existing = await _db.DashboardWidgets.Where(w => w.UserId == userId).ToListAsync();
        _db.DashboardWidgets.RemoveRange(existing);

        var newWidgets = widgets.Select((w, i) => new DashboardWidget
        {
            UserId    = userId,
            WidgetKey = w.key,
            SortOrder = i,
            ColSpan   = w.colSpan
        }).ToList();

        await _db.DashboardWidgets.AddRangeAsync(newWidgets);
        await _db.SaveChangesAsync();
    }

    public WidgetDefinition? GetDefinition(string key) =>
        Catalog.FirstOrDefault(c => c.Key == key);
}
