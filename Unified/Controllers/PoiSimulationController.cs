using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Unified.Models.Identity;
using Unified.Models.Poi;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class PoiSimulationController : Controller
{
    private readonly PoiSimulationService _svc;
    private readonly UserManager<AppUser> _users;

    public PoiSimulationController(PoiSimulationService svc, UserManager<AppUser> users)
    {
        _svc   = svc;
        _users = users;
    }

    // GET /PoiSimulation — filterable log for all users
    public async Task<IActionResult> Index(int? brandId, DateTime? from, DateTime? to, PoiStatus? status)
    {
        var sims   = await _svc.GetFilteredAsync(brandId, from, to, status);
        var brands = await _svc.GetBrandsAsync();

        ViewBag.Simulations = sims;
        ViewBag.Brands      = brands;
        ViewBag.BrandId     = brandId;
        ViewBag.From        = from;
        ViewBag.To          = to;
        ViewBag.Status      = status;
        return View();
    }

    // GET /PoiSimulation/Log — new simulation form
    public async Task<IActionResult> Log()
    {
        ViewBag.Brands = await _svc.GetBrandsAsync();
        return View();
    }

    // GET /PoiSimulation/LogPartial — modal form for dashboard
    [HttpGet]
    public async Task<IActionResult> LogPartial()
    {
        ViewBag.Brands = await _svc.GetBrandsAsync();
        return PartialView("~/Unified/Views/PoiSimulation/_LogPartial.cshtml");
    }

    // POST /PoiSimulation/Log
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Log(string clientId, int brandId, string notes)
    {
        var isAjax = string.Equals(Request.Headers[HeaderNames.XRequestedWith], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(clientId))
        {
            ModelState.AddModelError("", "Client ID is required.");
            ViewBag.Brands = await _svc.GetBrandsAsync();

            if (isAjax)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return PartialView("~/Unified/Views/PoiSimulation/_LogPartial.cshtml");
            }

            return View();
        }

        var userId = _users.GetUserId(User)!;
        await _svc.LogSimulationAsync(clientId, brandId, userId, notes ?? string.Empty);

        TempData["Success"] = $"POI simulation logged for client {clientId}.";

        if (isAjax)
            return Json(new { success = true, message = $"POI simulation logged for client {clientId}." });

        return RedirectToAction(nameof(Index));
    }

    // POST /PoiSimulation/MarkReceived
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReceived(int id, string? returnUrl)
    {
        var userId = _users.GetUserId(User)!;
        var ok     = await _svc.MarkReceivedAsync(id, userId);

        TempData[ok ? "Success" : "Info"] = ok
            ? "POI marked as received."
            : "POI was already marked as received or not found.";

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = ok, message = ok ? "POI marked as received." : "POI was already marked or not found." });

        return string.IsNullOrEmpty(returnUrl)
            ? RedirectToAction(nameof(Index))
            : Redirect(returnUrl);
    }

    // GET /PoiSimulation/Report — BrandManager / TeamLeader only
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Report(int? brandId, DateTime? from, DateTime? to)
    {
        var f      = from?.Date ?? DateTime.Today.AddDays(-30);
        var t      = to?.Date   ?? DateTime.Today;
        var sims   = await _svc.GetReportAsync(brandId, f, t);
        var brands = await _svc.GetBrandsAsync();

        // Group by brand for the report table
        var grouped = sims
            .GroupBy(p => p.Brand?.Name ?? "Unknown")
            .ToDictionary(
                g => g.Key,
                g => g.ToList());

        ViewBag.Grouped  = grouped;
        ViewBag.Brands   = brands;
        ViewBag.BrandId  = brandId;
        ViewBag.From     = f;
        ViewBag.To       = t;
        ViewBag.TotalSims      = sims.Count;
        ViewBag.TotalReceived  = sims.Count(s => s.PoiReceived);
        ViewBag.TotalRestricted= sims.Count(s => s.Status == PoiStatus.Restricted);
        return View();
    }
}
