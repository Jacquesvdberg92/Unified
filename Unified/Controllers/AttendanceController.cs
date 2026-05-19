using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Unified.Models.Attendance;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class AttendanceController : Controller
{
    private readonly AttendanceService _svc;
    private readonly UserManager<AppUser> _users;

    public AttendanceController(AttendanceService svc, UserManager<AppUser> users)
    {
        _svc   = svc;
        _users = users;
    }

    // GET /Attendance — agent dashboard
    public async Task<IActionResult> Index()
    {
        var userId = _users.GetUserId(User)!;
        var today  = await _svc.GetTodayLogAsync(userId);

        var from    = DateTime.UtcNow.AddDays(-30).Date;
        var to      = DateTime.UtcNow.Date;
        var history = await _svc.GetAgentHistoryAsync(userId, from, to);

        ViewBag.Today   = today;
        ViewBag.History = history;
        return View();
    }

    // POST /Attendance/ClockIn
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ClockIn()
    {
        var userId = _users.GetUserId(User)!;
        try
        {
            await _svc.ClockInAsync(userId);
            TempData["Success"] = "Clocked in successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // POST /Attendance/ClockOut
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ClockOut()
    {
        var userId = _users.GetUserId(User)!;
        try
        {
            await _svc.ClockOutAsync(userId);
            TempData["Success"] = "Clocked out successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // GET /Attendance/EditLog/5
    [HttpGet]
    public async Task<IActionResult> EditLog(int id)
    {
        var userId = _users.GetUserId(User)!;
        var log = await _svc.GetLogByIdAsync(id);
        if (log == null || log.AgentId != userId) return NotFound();
        return View(log);
    }

    // POST /Attendance/EditLog/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLog(int id, string? checkIn, string? checkOut)
    {
        var userId = _users.GetUserId(User)!;
        var log    = await _svc.GetLogByIdAsync(id);
        if (log == null || log.AgentId != userId) return NotFound();

        DateTime? parsedIn  = null;
        DateTime? parsedOut = null;

        if (!string.IsNullOrWhiteSpace(checkIn) && TimeSpan.TryParse(checkIn, out var inTs))
            parsedIn = log.WorkDate.ToUniversalTime().Date + inTs;

        if (!string.IsNullOrWhiteSpace(checkOut) && TimeSpan.TryParse(checkOut, out var outTs))
            parsedOut = log.WorkDate.ToUniversalTime().Date + outTs;

        try
        {
            await _svc.UpdateTimesAsync(id, userId, parsedIn, parsedOut);
            TempData["Success"] = "Times updated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    // GET /Attendance/Retrospective
    public IActionResult Retrospective() => View();

    // POST /Attendance/Retrospective
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Retrospective(
        DateTime workDate,
        DateTime checkIn,
        DateTime checkOut,
        string? note)
    {
        if (checkOut <= checkIn)
        {
            ModelState.AddModelError(string.Empty, "Check-out must be after check-in.");
            return View();
        }

        var userId = _users.GetUserId(User)!;
        try
        {
            await _svc.SubmitRetrospectiveAsync(userId, workDate, checkIn, checkOut, note);
            TempData["Success"] = "Missed punch saved successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View();
        }
    }
}
