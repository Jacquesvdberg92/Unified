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
    private readonly AttendanceService    _attendance;

    public ScheduleController(ScheduleService service, AppDbContext db, UserManager<AppUser> users, AttendanceService attendance)
    {
        _service    = service;
        _db         = db;
        _users      = users;
        _attendance = attendance;
    }

    // ── Week view (leaders/admins) ─────────────────────────────────────────

    public async Task<IActionResult> WeekView(string? weekStart)
    {
        var week   = ParseWeek(weekStart);
        var shifts = await _service.GetShiftTemplatesAsync();
        var teams  = await _db.Teams.OrderBy(t => t.Name).ToListAsync();

        // Load ALL schedules for the week (all agents across all teams)
        var weekEnd    = week.AddDays(7);
        var allScheds  = await _db.AgentSchedules
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Date >= week && s.Date < weekEnd)
            .ToListAsync();

        // Build role-grouped data
        // managerGroup: all Brand Managers
        // teamSections: per-team list of (leaders, agents)
        var managerGroup   = new List<AppUser>();
        // teamId -> (leaders, agents)
        var teamSections   = new List<(Team Team, List<AppUser> Leaders, List<AppUser> Agents)>();

        var allUsers = await _db.Users.OrderBy(u => u.DisplayName).ToListAsync();

        // Collect all user roles efficiently
        var userRoleMap = new Dictionary<string, IList<string>>();
        foreach (var u in allUsers)
            userRoleMap[u.Id] = await _users.GetRolesAsync(u);

        // Managers (cross-team)
        managerGroup = allUsers.Where(u => userRoleMap[u.Id].Contains(Roles.BrandManager)).ToList();

        // Per-team: leaders + agents
        foreach (var team in teams)
        {
            var memberIds = await _db.AgentTeams
                .Where(at => at.TeamId == team.Id)
                .Select(at => at.AgentId)
                .ToListAsync();

            var members = allUsers.Where(u => memberIds.Contains(u.Id)).ToList();
            var leaders = members.Where(u => userRoleMap[u.Id].Contains(Roles.TeamLeader)).ToList();
            var agents  = members.Where(u => !userRoleMap[u.Id].Contains(Roles.TeamLeader)
                                          && !userRoleMap[u.Id].Contains(Roles.BrandManager)).ToList();
            teamSections.Add((team, leaders, agents));
        }

        // Pending count (all teams combined) — leaders/managers only
        var canEdit = User.IsInRole(Roles.BrandManager) || User.IsInRole(Roles.TeamLeader);
        var pendingCount = canEdit
            ? await _db.TimeOffRequests.CountAsync(r => r.Status == Unified.Models.Schedule.TimeOffStatus.Pending)
            : 0;

        ViewBag.WeekStart      = week;
        ViewBag.ShiftTemplates = shifts;
        ViewBag.PendingCount   = pendingCount;
        ViewBag.ManagerGroup   = managerGroup;
        ViewBag.TeamSections   = teamSections;
        ViewBag.CanEdit        = canEdit;

        return View(allScheds);
    }

    // POST: assign / edit a single day
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> SetAgentDay(AgentSchedule entry, string weekStart)
    {
        ModelState.Remove(nameof(entry.Agent));
        ModelState.Remove(nameof(entry.ShiftTemplate));

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid schedule entry.";
            return RedirectToAction(nameof(WeekView), new { weekStart });
        }

        await _service.SetAgentDayAsync(entry);
        TempData["Success"] = "Schedule updated.";
        return RedirectToAction(nameof(WeekView), new { weekStart });
    }

    // ── Agent personal view ────────────────────────────────────────────────

    public async Task<IActionResult> AgentView(string? weekStart)
    {
        var userId = _users.GetUserId(User)!;
        var week   = ParseWeek(weekStart);
        var schedules = await _service.GetAgentScheduleAsync(userId, week, week.AddDays(6));
        var allShifts = await _service.GetShiftTemplatesAsync();

        // Load the agent's team so the full schedule can be shown below
        var agentTeamId = await _db.AgentTeams
            .Where(at => at.AgentId == userId)
            .Select(at => (int?)at.TeamId)
            .FirstOrDefaultAsync();

        List<AgentSchedule> teamSchedules = new();
        List<AppUser>       teamMembers   = new();
        if (agentTeamId.HasValue)
        {
            teamSchedules = await _service.GetWeeklyScheduleAsync(agentTeamId.Value, week);
            teamMembers   = await GetTeamAgentsAsync(agentTeamId.Value);
        }

        ViewBag.WeekStart         = week;
        ViewBag.ShiftTemplates    = allShifts;
        ViewBag.WeekdayShifts     = allShifts.Where(s => !s.IsWeekendShift).ToList();
        ViewBag.WeekendShifts     = allShifts.Where(s => s.IsWeekendShift).ToList();
        ViewBag.TeamSchedules     = teamSchedules;
        ViewBag.TeamMembers       = teamMembers;
        ViewBag.TodayLog          = await _attendance.GetTodayLogAsync(userId);
        return View(schedules);
    }

    // POST: agent self-assigns a single day on their own schedule
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMyDay(AgentSchedule entry, string weekStart)
    {
        ModelState.Remove(nameof(entry.Agent));
        ModelState.Remove(nameof(entry.ShiftTemplate));

        entry.AgentId = _users.GetUserId(User)!;

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid schedule entry.";
            return RedirectToAction(nameof(AgentView), new { weekStart });
        }

        await _service.SetAgentDayAsync(entry);
        TempData["Success"] = "Your schedule was updated.";
        return RedirectToAction(nameof(AgentView), new { weekStart });
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

    // ── Shift Template Management ─────────────────────────────────────────

    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> ShiftTemplates()
    {
        var templates = await _db.ShiftTemplates.OrderBy(t => t.StartTime).ToListAsync();
        return View(templates);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> CreateShiftTemplate(ShiftTemplate model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid shift template data.";
            return RedirectToAction(nameof(ShiftTemplates));
        }

        _db.ShiftTemplates.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Shift \"{model.Name}\" created.";
        return RedirectToAction(nameof(ShiftTemplates));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> EditShiftTemplate(ShiftTemplate model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid shift template data.";
            return RedirectToAction(nameof(ShiftTemplates));
        }

        var existing = await _db.ShiftTemplates.FindAsync(model.Id);
        if (existing is null)
        {
            TempData["Error"] = "Shift template not found.";
            return RedirectToAction(nameof(ShiftTemplates));
        }

        existing.Name          = model.Name;
        existing.StartTime     = model.StartTime;
        existing.EndTime       = model.EndTime;
        existing.IsWeekendShift = model.IsWeekendShift;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Shift \"{model.Name}\" updated.";
        return RedirectToAction(nameof(ShiftTemplates));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> DeleteShiftTemplate(int id)
    {
        var template = await _db.ShiftTemplates.FindAsync(id);
        if (template is not null)
        {
            _db.ShiftTemplates.Remove(template);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Shift \"{template.Name}\" deleted.";
        }
        return RedirectToAction(nameof(ShiftTemplates));
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
