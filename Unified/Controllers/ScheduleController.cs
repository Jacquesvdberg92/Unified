using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.Schedule;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class ScheduleController : Controller
{
    private readonly ScheduleService      _service;
    private readonly AppDbContext         _db;
    private readonly UserManager<AppUser> _users;

    public ScheduleController(ScheduleService service, AppDbContext db, UserManager<AppUser> users)
    {
        _service = service;
        _db      = db;
        _users   = users;
    }

    // ── Week view (leaders/admins) ─────────────────────────────────────────

    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> WeekView(int? teamId, string? weekStart)
    {
        var teams = await _db.Teams.OrderBy(t => t.Name).ToListAsync();
        var selTeamId = teamId ?? teams.FirstOrDefault()?.Id ?? 0;
        var week = ParseWeek(weekStart);

        var schedules      = selTeamId > 0 ? await _service.GetWeeklyScheduleAsync(selTeamId, week) : new();
        var shifts         = await _service.GetShiftTemplatesAsync();
        var pendingCount   = selTeamId > 0 ? await _service.GetPendingCountForTeamAsync(selTeamId) : 0;
        var teamAgents     = selTeamId > 0 ? await GetTeamAgentsAsync(selTeamId) : new();

        ViewBag.Teams          = teams;
        ViewBag.SelectedTeamId = selTeamId;
        ViewBag.WeekStart      = week;
        ViewBag.ShiftTemplates = shifts;
        ViewBag.PendingCount   = pendingCount;
        ViewBag.TeamAgents     = teamAgents;

        return View(schedules);
    }

    // POST: assign / edit a single day
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> SetAgentDay(AgentSchedule entry, int teamId, string weekStart)
    {
        ModelState.Remove(nameof(entry.Agent));
        ModelState.Remove(nameof(entry.ShiftTemplate));

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid schedule entry.";
            return RedirectToAction(nameof(WeekView), new { teamId, weekStart });
        }

        await _service.SetAgentDayAsync(entry);
        TempData["Success"] = "Schedule updated.";
        return RedirectToAction(nameof(WeekView), new { teamId, weekStart });
    }

    // ── Agent personal view ────────────────────────────────────────────────

    public async Task<IActionResult> AgentView(string? weekStart)
    {
        var userId = _users.GetUserId(User)!;
        var week   = ParseWeek(weekStart);
        var schedules = await _service.GetAgentScheduleAsync(userId, week, week.AddDays(6));
        var shifts    = await _service.GetShiftTemplatesAsync();

        ViewBag.WeekStart      = week;
        ViewBag.ShiftTemplates = shifts;
        return View(schedules);
    }

    // ── Weekend wheel ──────────────────────────────────────────────────────

    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> WeekendWheel(string? weekStart)
    {
        var week       = ParseWeek(weekStart);
        var candidates = await _service.SpinWheelCandidatesAsync(week);
        var pastOffers = await _service.GetOffersForWeekAsync(week);

        ViewBag.WeekStart  = week;
        ViewBag.PastOffers = pastOffers;
        return View(candidates);
    }

    // POST: record an offer
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> RecordOffer(string agentId, string weekStart)
    {
        var leaderId = _users.GetUserId(User)!;
        var week     = DateTime.Parse(weekStart);
        await _service.RecordOfferAsync(agentId, week, leaderId);
        TempData["Success"] = "Offer recorded.";
        return RedirectToAction(nameof(WeekendWheel), new { weekStart });
    }

    // POST: mark offer accepted
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> AcceptOffer(int offerId, string weekStart)
    {
        await _service.MarkOfferAcceptedAsync(offerId);
        TempData["Success"] = "Offer marked as accepted.";
        return RedirectToAction(nameof(WeekendWheel), new { weekStart });
    }

    // ── Agent time-off requests ────────────────────────────────────────────

    public async Task<IActionResult> MyRequests()
    {
        var userId   = _users.GetUserId(User)!;
        var requests = await _service.GetMyRequestsAsync(userId);

        // Toast when a request has just been actioned (new review since last visit)
        var newlyActioned = requests
            .Where(r => r.Status != TimeOffStatus.Pending &&
                        r.ReviewedAt > (DateTime?)TempData["LastRequestsCheck"])
            .Count();
        TempData["LastRequestsCheck"] = DateTime.UtcNow;

        ViewBag.NewlyActioned = newlyActioned;
        return View(requests);
    }

    // POST: submit a new request
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitRequest(TimeOffRequest request)
    {
        ModelState.Remove(nameof(request.Agent));
        ModelState.Remove(nameof(request.ReviewedByLeader));

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please fill in all required fields.";
            return RedirectToAction(nameof(MyRequests));
        }

        request.AgentId = _users.GetUserId(User)!;

        try
        {
            await _service.SubmitTimeOffRequestAsync(request);
            TempData["Success"] = "Request submitted successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(MyRequests));
    }

    // ── Leader review queue ────────────────────────────────────────────────

    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> ReviewRequests(int? teamId)
    {
        var teams     = await _db.Teams.OrderBy(t => t.Name).ToListAsync();
        var selTeamId = teamId ?? teams.FirstOrDefault()?.Id ?? 0;
        var requests  = selTeamId > 0 ? await _service.GetPendingRequestsAsync(selTeamId) : new();

        ViewBag.Teams          = teams;
        ViewBag.SelectedTeamId = selTeamId;
        return View(requests);
    }

    // POST: approve
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> ApproveRequest(int requestId, string? leaderNote, int teamId)
    {
        var leaderId = _users.GetUserId(User)!;
        await _service.ApproveRequestAsync(requestId, leaderId, leaderNote);
        TempData["Success"] = "Request approved.";
        return RedirectToAction(nameof(ReviewRequests), new { teamId });
    }

    // POST: deny
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> DenyRequest(int requestId, string? leaderNote, int teamId)
    {
        var leaderId = _users.GetUserId(User)!;
        await _service.DenyRequestAsync(requestId, leaderId, leaderNote);
        TempData["Success"] = "Request denied.";
        return RedirectToAction(nameof(ReviewRequests), new { teamId });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static DateTime ParseWeek(string? raw)
    {
        if (DateTime.TryParse(raw, out var d))
        {
            // Snap to Monday
            int diff = ((int)d.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return d.Date.AddDays(-diff);
        }
        // Default: current week's Monday
        var today = DateTime.Today;
        int delta = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return today.AddDays(-delta);
    }

    private async Task<List<AppUser>> GetTeamAgentsAsync(int teamId)
    {
        var memberIds = await _db.AgentTeams
            .Where(at => at.TeamId == teamId)
            .Select(at => at.AgentId)
            .ToListAsync();

        var sakIds = await _db.Users
            .Where(u => u.IsSwissArmyKnife)
            .Select(u => u.Id)
            .ToListAsync();

        var allIds = memberIds.Union(sakIds).Distinct().ToList();
        return await _db.Users
            .Where(u => allIds.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .ToListAsync();
    }
}
