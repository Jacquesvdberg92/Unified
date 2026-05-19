using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Unified.Models.Attendance;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader},{Roles.SwissArmyKnife}")]
public class AttendanceReportController : Controller
{
    private readonly AttendanceService  _svc;
    private readonly ScheduleService    _schedule;
    private readonly UserManager<AppUser> _users;

    public AttendanceReportController(
        AttendanceService svc,
        ScheduleService schedule,
        UserManager<AppUser> users)
    {
        _svc      = svc;
        _schedule = schedule;
        _users    = users;
    }

    // ── Report ───────────────────────────────────────────────────────────────

    // GET /AttendanceReport
    public async Task<IActionResult> Index(
        string period = "weekly",
        string? agentId = null,
        DateTime? customFrom = null,
        DateTime? customTo   = null)
    {
        DateTime from, to;

        if (period == "custom" && customFrom.HasValue && customTo.HasValue)
        {
            from = customFrom.Value.Date;
            to   = customTo.Value.Date;
        }
        else
        {
            (from, to) = ResolvePeriod(period);
        }

        var rows      = await _svc.GenerateReportAsync(from, to, agentId);
        var allAgents = _users.Users
            .OrderBy(u => u.DisplayName ?? u.UserName)
            .ToList();

        ViewBag.Period      = period;
        ViewBag.From        = from;
        ViewBag.To          = to;
        ViewBag.AgentId     = agentId;
        ViewBag.AllAgents   = allAgents;
        ViewBag.CustomFrom  = customFrom?.ToString("yyyy-MM-dd") ?? from.ToString("yyyy-MM-dd");
        ViewBag.CustomTo    = customTo?.ToString("yyyy-MM-dd")   ?? to.ToString("yyyy-MM-dd");
        return View(rows);
    }

    // ── Retrospective approval queue ─────────────────────────────────────────

    // GET /AttendanceReport/EditLog/5  — manager / TL fix an agent's times
    [HttpGet]
    public async Task<IActionResult> EditLog(int id)
    {
        var log = await _svc.GetLogByIdAsync(id);
        if (log == null) return NotFound();

        var agent = await _users.FindByIdAsync(log.AgentId);
        ViewBag.AgentName = agent?.DisplayName ?? agent?.UserName ?? log.AgentId;

        // Load schedule entry for that day so the fixer can see the shift
        var scheduleEntries = await _schedule.GetAgentScheduleAsync(log.AgentId, log.WorkDate, log.WorkDate);
        ViewBag.ScheduleEntry = scheduleEntries.FirstOrDefault();

        return View(log);
    }

    // POST /AttendanceReport/EditLog/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLog(int id, string? checkIn, string? checkOut)
    {
        var log = await _svc.GetLogByIdAsync(id);
        if (log == null) return NotFound();

        DateTime? parsedIn  = null;
        DateTime? parsedOut = null;

        if (!string.IsNullOrWhiteSpace(checkIn) && TimeSpan.TryParse(checkIn, out var inTs))
            parsedIn = log.WorkDate.Date + inTs;

        if (!string.IsNullOrWhiteSpace(checkOut) && TimeSpan.TryParse(checkOut, out var outTs))
            parsedOut = log.WorkDate.Date + outTs;

        try
        {
            await _svc.ManagerUpdateTimesAsync(id, parsedIn, parsedOut);
            TempData["Success"] = "Times updated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // GET /AttendanceReport/Retrospective
    public async Task<IActionResult> Retrospective()
    {
        var pending = await _svc.GetPendingRetrospectivesAsync();
        return View(pending);
    }

    // POST /AttendanceReport/Approve
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int logId, string? reviewerNote)
    {
        var reviewerId = _users.GetUserId(User)!;
        await _svc.ReviewRetrospectiveAsync(logId, reviewerId, approved: true, reviewerNote);
        TempData["Success"] = "Entry approved.";
        return RedirectToAction(nameof(Retrospective));
    }

    // POST /AttendanceReport/Reject
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int logId, string? reviewerNote)
    {
        var reviewerId = _users.GetUserId(User)!;
        await _svc.ReviewRetrospectiveAsync(logId, reviewerId, approved: false, reviewerNote);
        TempData["Error"] = "Entry rejected.";
        return RedirectToAction(nameof(Retrospective));
    }

    // ── Public Holidays ──────────────────────────────────────────────────────

    // GET /AttendanceReport/Holidays
    public async Task<IActionResult> Holidays(int year = 0)
    {
        if (year == 0) year = DateTime.UtcNow.Year;
        var holidays = await _svc.GetHolidaysAsync(year);
        ViewBag.Year = year;
        return View(holidays);
    }

    // POST /AttendanceReport/AddHoliday
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddHoliday(DateTime date, string name, string? notes)
    {
        await _svc.AddHolidayAsync(new PublicHoliday { Date = date.Date, Name = name, Notes = notes });
        TempData["Success"] = $"{name} added.";
        return RedirectToAction(nameof(Holidays), new { year = date.Year });
    }

    // POST /AttendanceReport/DeleteHoliday
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHoliday(int id, int year)
    {
        await _svc.DeleteHolidayAsync(id);
        TempData["Success"] = "Holiday removed.";
        return RedirectToAction(nameof(Holidays), new { year });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (DateTime from, DateTime to) ResolvePeriod(string period)
    {
        var today = DateTime.UtcNow.Date;

        // Days since Monday: Monday=0, Tuesday=1 ... Sunday=6
        int daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday
            ? 6
            : (int)today.DayOfWeek - 1;

        return period switch
        {
            "biweekly" => (today.AddDays(-13), today),
            "monthly"  => (new DateTime(today.Year, today.Month, 1), today),
            _          => (today.AddDays(-daysSinceMonday), today) // weekly Mon–today
        };
    }
}
