using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class CsLiveHelpController : Controller
{
    private readonly CsLiveHelpService   _svc;
    private readonly UserManager<AppUser> _users;

    public CsLiveHelpController(CsLiveHelpService svc, UserManager<AppUser> users)
    {
        _svc   = svc;
        _users = users;
    }

    // GET /CsLiveHelp  — view today's schedule
    public async Task<IActionResult> Index(DateTime? date)
    {
        var d        = date?.Date ?? DateTime.Today;
        var slots    = await _svc.GetSlotsForDateAsync(d);
        var eligible = await _svc.GetEligibleAgentsAsync();
        ViewBag.Date      = d;
        ViewBag.Slots     = slots;
        ViewBag.SlotHours = CsLiveHelpService.SlotHours;
        ViewBag.Eligible  = eligible;
        return View();
    }

    // GET /CsLiveHelp/Generate  — managers build the day's schedule
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Generate(DateTime? date)
    {
        var d        = date?.Date ?? DateTime.Today;
        var slots    = await _svc.GetSlotsForDateAsync(d);
        var eligible = await _svc.GetEligibleAgentsAsync();

        ViewBag.Date      = d;
        ViewBag.Slots     = slots;
        ViewBag.SlotHours = CsLiveHelpService.SlotHours;
        ViewBag.Eligible  = eligible;
        return View();
    }

    // POST /CsLiveHelp/Generate
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Generate(DateTime date, IFormCollection form)
    {
        var assignments = new Dictionary<int, (string? a1, string? a2)>();

        foreach (var hour in CsLiveHelpService.SlotHours)
        {
            var a1 = form[$"slot_{hour}_agent1"].ToString();
            var a2 = form[$"slot_{hour}_agent2"].ToString();
            assignments[hour] = (a1, a2);
        }

        var userId = _users.GetUserId(User)!;
        await _svc.GenerateScheduleAsync(date, assignments, userId);

        TempData["Success"] = $"CS Live Help schedule for {date:dd MMM yyyy} saved.";
        return RedirectToAction("Index", "WorkDistribution", new { date = date.ToString("yyyy-MM-dd") });
    }

    // POST /CsLiveHelp/Swap  — any logged-in user can request/perform a swap
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Swap(int slotId, int agentPosition, string? newAgentId, string? reason, DateTime returnDate)
    {
        var userId = _users.GetUserId(User)!;
        await _svc.SwapAgentAsync(slotId, agentPosition, newAgentId, userId, reason ?? string.Empty);

        TempData["Success"] = "Swap recorded successfully.";
        return RedirectToAction("Index", "WorkDistribution", new { date = returnDate.ToString("yyyy-MM-dd") });
    }

    // GET /CsLiveHelp/SwapLog
    public async Task<IActionResult> SwapLog(DateTime? date)
    {
        var logs = await _svc.GetSwapLogAsync(date);
        ViewBag.FilterDate = date;
        ViewBag.Logs       = logs;
        return View();
    }
}
