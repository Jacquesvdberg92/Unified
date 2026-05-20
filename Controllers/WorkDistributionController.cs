using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Unified.Models.Identity;
using Unified.Services;


namespace Unified.Controllers;

[Authorize]
public class WorkDistributionController : Controller
{
    private readonly WorkDistributionService  _svc;
    private readonly CsLiveAllocationService  _csLiveHelp;
    private readonly UserManager<AppUser>     _users;

    public WorkDistributionController(WorkDistributionService svc, CsLiveAllocationService csLiveHelp, UserManager<AppUser> users)
    {
        _svc        = svc;
        _csLiveHelp = csLiveHelp;
        _users      = users;
    }

    // GET /WorkDistribution  — unified page for a given date
    public async Task<IActionResult> Index(DateTime? date)
    {
        var d      = date?.Date ?? DateTime.Today;
        var entry  = await _svc.GetForDateAsync(d);
        var recent = await _svc.GetRecentAsync(14);
        var slots  = await _csLiveHelp.GetSlotsForDateAsync(d);
        var eligible = await _csLiveHelp.GetEligibleAgentsAsync();

        ViewBag.Date        = d;
        ViewBag.TodaysEntry = entry;
        ViewBag.Recent      = recent;
        ViewBag.Slots       = slots;
        ViewBag.SlotHours   = CsLiveAllocationService.SlotHours;
        ViewBag.Eligible    = eligible;
        return View();
    }

    // GET /WorkDistribution/View/2025-05-19  — view a specific day
    [Route("WorkDistribution/View/{date}")]
    public async Task<IActionResult> ViewDate(DateTime date)
    {
        var entry = await _svc.GetForDateAsync(date);
        if (entry == null)
        {
            TempData["Info"] = $"No work distribution found for {date:dd MMM yyyy}.";
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Entry = entry;
        return View("ViewDate");
    }

    // GET /WorkDistribution/Edit/2025-05-19  — managers only
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    [Route("WorkDistribution/Edit/{date?}")]
    public async Task<IActionResult> Edit(DateTime? date)
    {
        var d     = date?.Date ?? DateTime.Today;
        var entry = await _svc.GetForDateAsync(d);
        ViewBag.Date = d;
        ViewBag.Body = entry?.Body ?? string.Empty;
        return View();
    }

    // POST /WorkDistribution/Save
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Save(DateTime date, string body)
    {
        var userId = _users.GetUserId(User)!;
        await _svc.SaveAsync(date, body, userId);
        TempData["Success"] = $"Work distribution for {date:dd MMM yyyy} saved.";
        return RedirectToAction(nameof(Index));
    }

    // POST /WorkDistribution/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteAsync(id);
        TempData["Success"] = "Entry deleted.";
        return RedirectToAction(nameof(Index));
    }
}
