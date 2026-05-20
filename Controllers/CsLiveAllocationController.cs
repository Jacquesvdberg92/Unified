using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class CsLiveAllocationController : Controller
{
    private readonly CsLiveAllocationService   _svc;
    private readonly UserManager<AppUser> _users;

    public CsLiveAllocationController(CsLiveAllocationService svc, UserManager<AppUser> users)
    {
        _svc   = svc;
        _users = users;
    }

    // GET /CsLiveAllocation  — view today's allocation
    public async Task<IActionResult> Index(DateTime? date)
    {
        var d        = date?.Date ?? DateTime.Today;
        var slots    = await _svc.GetSlotsForDateAsync(d);
        var eligible = await _svc.GetEligibleAgentsAsync();
        ViewBag.Date      = d;
        ViewBag.Slots     = slots;
        ViewBag.SlotHours = CsLiveAllocationService.SlotHours;
        ViewBag.Eligible  = eligible;
        return View();
    }

    // GET /CsLiveAllocation/Generate  — managers build the day's allocation
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Generate(DateTime? date)
    {
        var d        = date?.Date ?? DateTime.Today;
        var slots    = await _svc.GetSlotsForDateAsync(d);
        var eligible = await _svc.GetEligibleAgentsAsync();

        ViewBag.Date      = d;
        ViewBag.Slots     = slots;
        ViewBag.SlotHours = CsLiveAllocationService.SlotHours;
        ViewBag.Eligible  = eligible;
        return View();
    }

    // POST /CsLiveAllocation/Generate
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Generate(DateTime date, IFormCollection form)
    {
        var assignments = new Dictionary<int, (string? a1, string? a2)>();

        foreach (var hour in CsLiveAllocationService.SlotHours)
        {
            var a1 = form[$"slot_{hour}_agent1"].ToString();
            var a2 = form[$"slot_{hour}_agent2"].ToString();
            assignments[hour] = (a1, a2);
        }

        var userId = _users.GetUserId(User)!;
        await _svc.GenerateScheduleAsync(date, assignments, userId);

        TempData["Success"] = $"CS Live Allocation schedule for {date:dd MMM yyyy} saved.";
        return RedirectToAction("Index", "WorkDistribution", new { date = date.ToString("yyyy-MM-dd") });
    }

    // POST /CsLiveAllocation/Swap  — any logged-in user can request/perform a swap
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Swap(int slotId, int agentPosition, string? newAgentId, string? reason, DateTime returnDate)
    {
        var userId = _users.GetUserId(User)!;
        await _svc.SwapAgentAsync(slotId, agentPosition, newAgentId, userId, reason ?? string.Empty);

        TempData["Success"] = "Swap recorded successfully.";
        return RedirectToAction("Index", "WorkDistribution", new { date = returnDate.ToString("yyyy-MM-dd") });
    }

    // GET /CsLiveAllocation/SwapLog
    public async Task<IActionResult> SwapLog(DateTime? date)
    {
        var logs = await _svc.GetSwapLogAsync(date);
        ViewBag.FilterDate = date;
        ViewBag.Logs       = logs;
        return View();
    }
}
