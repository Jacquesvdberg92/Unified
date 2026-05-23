using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Unified.Models;
using Unified.Models.Identity;
using Unified.Services;
using Unified.Models.Dashboard;

namespace Unified.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UpdateService           _updateService;
    private readonly SignInManager<AppUser>  _signInManager;
    private readonly DashboardService        _dashboardService;
    private readonly UserManager<AppUser>    _userManager;
    private readonly TelegramService         _telegramService;

    // In-memory rate-limit: userId → last request time (max 1 per 5 min)
    private static readonly ConcurrentDictionary<string, DateTime> _loginRequestTimes = new();

    public HomeController(
        ILogger<HomeController> logger,
        UpdateService updateService,
        SignInManager<AppUser> signInManager,
        DashboardService dashboardService,
        UserManager<AppUser> userManager,
        TelegramService telegramService)
    {
        _logger           = logger;
        _updateService    = updateService;
        _signInManager    = signInManager;
        _dashboardService = dashboardService;
        _userManager      = userManager;
        _telegramService  = telegramService;
    }

    [Route("/")]
    [Route("/index")]
    public async Task<IActionResult> Index()
    {
        // Show pinned-updates toast on first page load after login.
        if (HttpContext.Session.GetString("PinnedToastShown") == null)
        {
            HttpContext.Session.SetString("PinnedToastShown", "1");
            var lastLogin = HttpContext.Session.GetString("LastLoginAt");
            DateTime? since = lastLogin is not null ? DateTime.Parse(lastLogin) : (DateTime?)null;
            var count = await _updateService.CountPinnedSinceAsync(since);
            if (count > 0)
                TempData["PinnedUpdatesCount"] = count;
            HttpContext.Session.SetString("LastLoginAt", DateTime.UtcNow.ToString("O"));
        }

        var userId = _userManager.GetUserId(User)!;
        var widgets = await _dashboardService.GetUserWidgetsAsync(userId);

        // First-time user: give them a default set of widgets
        if (!widgets.Any())
        {
            var defaults = new List<(string, int)>
            {
                ("updates_feed",      12),
                ("quick_links",       12),
                ("work_distribution", 6),
                ("my_schedule",       6),
            };
            await _dashboardService.SaveUserWidgetsAsync(userId, defaults);
            widgets = await _dashboardService.GetUserWidgetsAsync(userId);
        }

        ViewBag.Widgets = widgets;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Customize()
    {
        var userId  = _userManager.GetUserId(User)!;
        var active  = await _dashboardService.GetUserWidgetsAsync(userId);
        var catalog = DashboardService.Catalog;
        ViewBag.ActiveWidgets = active;
        ViewBag.Catalog       = catalog;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLayout(List<string> widgetKeys, List<int> colSpans)
    {
        var userId = _userManager.GetUserId(User)!;

        widgetKeys ??= new List<string>();
        colSpans   ??= new List<int>();

        var pairs = widgetKeys
            .Select((k, i) => (key: k, colSpan: i < colSpans.Count ? colSpans[i] : 6))
            .ToList();

        await _dashboardService.SaveUserWidgetsAsync(userId, pairs);
        TempData["Success"] = "Dashboard saved!";
        return RedirectToAction(nameof(Index));
    }


    // GET /Home/GetWidget/poi_simulations  — returns partial HTML for widget refresh
    [HttpGet]
    public IActionResult GetWidget(string key)
    {
        var partialName = key switch
        {
            "attendance"       => "Widgets/_Attendance",
            "poi_simulations"  => "Widgets/_PoiSimulations",
            "poi_report"       => "Widgets/_PoiReport",
            "updates_feed"     => "Widgets/_UpdatesFeed",
            "work_distribution"=> "Widgets/_WorkDistribution",
            "my_schedule"      => "Widgets/_MySchedule",
            "performance"      => "Widgets/_Performance",
            "reports"          => "Widgets/_Reports",
            "cs_live_help"     => "Widgets/_CsLiveAllocation",
            "quick_links"      => "Widgets/_QuickLinks",
            "request_login"    => "Widgets/_RequestLogin",
            _                  => null
        };

        if (partialName == null)
            return NotFound();

        return PartialView(partialName);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Roadmap()
    {
        return View();
    }

    /// <summary>
    /// Sends a "Please log me in" Telegram message with the user's AnyDesk ID.
    /// Rate-limited to 1 request per 5 minutes per user.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestLogin()
    {
        var userId = _userManager.GetUserId(User)!;

        // Rate-limit: max 1 request per 5 minutes per user
        if (_loginRequestTimes.TryGetValue(userId, out var last) &&
            (DateTime.UtcNow - last).TotalMinutes < 5)
        {
            var waitSeconds = (int)(300 - (DateTime.UtcNow - last).TotalSeconds);
            return Json(new { success = false, error = $"Please wait {waitSeconds} seconds before sending another request." });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Json(new { success = false, error = "User not found." });

        var (success, error) = await _telegramService.SendLoginRequestAsync(user.DisplayName, user.AnydeskId);

        if (success)
            _loginRequestTimes[userId] = DateTime.UtcNow;

        return Json(new { success, error });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode)
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
