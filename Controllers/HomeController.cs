using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Unified.Models;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UpdateService           _updateService;
    private readonly SignInManager<AppUser>  _signInManager;

    public HomeController(ILogger<HomeController> logger, UpdateService updateService, SignInManager<AppUser> signInManager)
    {
        _logger        = logger;
        _updateService = updateService;
        _signInManager = signInManager;
    }

    [Route("/")]
    [Route("/index")]
    public async Task<IActionResult> Index()
    {
        // Show pinned-updates toast on first page load after login.
        // We use a session flag so the toast fires once per login session.
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
        return View();
    }


    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode)
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
