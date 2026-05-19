using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.Reports;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ReportService        _svc;
    private readonly AppDbContext         _db;
    private readonly UserManager<AppUser> _users;
    private readonly ReferenceDataService _refData;

    public ReportsController(ReportService svc, AppDbContext db, UserManager<AppUser> users, ReferenceDataService refData)
    {
        _svc     = svc;
        _db      = db;
        _users   = users;
        _refData = refData;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────

    [HttpGet]
    [OutputCache(PolicyName = "Reports")]
    public async Task<IActionResult> Dashboard(int? periodType, string? periodStart)
    {
        var pt    = (PeriodType)(periodType ?? 0);
        var start = periodStart is not null
            ? DateTime.Parse(periodStart)
            : StartOfCurrentPeriod(pt);

        var summary  = await _svc.GetReportSummaryAsync(pt, start);
        var allDates = await GetAvailablePeriodDatesAsync(pt);

        ViewBag.PeriodType   = pt;
        ViewBag.PeriodStart  = start;
        ViewBag.PeriodTypes  = PeriodTypeSelectList((int)pt);
        ViewBag.Dates        = allDates;
        return View(summary);
    }

    // ── Submit ────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> Submit()
    {
        var leaderId = _users.GetUserId(User)!;
        ViewBag.Teams      = await GetTeamSelectListAsync(leaderId);
        ViewBag.Agents     = await GetAgentSelectListAsync(leaderId);
        ViewBag.PeriodTypes = PeriodTypeSelectList();
        return View(new TeamReport { PeriodStart = StartOfCurrentPeriod(PeriodType.Weekly) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> Submit(
        TeamReport report,
        string[]  agentId,
        int[]     chats,
        int[]     tickets,
        int[]     calls,
        int[]     ftd,
        string[]  language)
    {
        report.ReportedByLeaderId = _users.GetUserId(User)!;

        for (int i = 0; i < agentId.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(agentId[i])) continue;
            report.AgentStats.Add(new AgentStat
            {
                AgentId  = agentId[i],
                Chats    = chats.ElementAtOrDefault(i),
                Tickets  = tickets.ElementAtOrDefault(i),
                Calls    = calls.ElementAtOrDefault(i),
                FTD      = ftd.ElementAtOrDefault(i),
                Language = language.ElementAtOrDefault(i)
            });
        }

        if (!report.AgentStats.Any())
        {
            ModelState.AddModelError("", "At least one agent row is required.");
            await PopulateSubmitViewBagsAsync();
            return View(report);
        }

        try
        {
            await _svc.SubmitReportAsync(report);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await PopulateSubmitViewBagsAsync();
            return View(report);
        }

        TempData["Success"] = "Report submitted successfully.";
        return RedirectToAction(nameof(Dashboard));
    }

    // ── Detail ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var report = await _svc.GetTeamBreakdownAsync(id);
        if (report is null) return NotFound();

        var highlights = await _svc.GetPerformanceHighlightsAsync(id);
        ViewBag.Highlights = highlights;
        return View(report);
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteReportAsync(id);
        return RedirectToAction(nameof(Dashboard));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task PopulateSubmitViewBagsAsync()
    {
        var leaderId = _users.GetUserId(User)!;
        ViewBag.Teams       = await GetTeamSelectListAsync(leaderId);
        ViewBag.Agents      = await GetAgentSelectListAsync(leaderId);
        ViewBag.PeriodTypes = PeriodTypeSelectList();
    }

    private async Task<SelectList> GetTeamSelectListAsync(string leaderId)
    {
        List<Team> teams;
        if (User.IsInRole(Roles.BrandManager))
        {
            teams = await _refData.GetTeamsAsync();
        }
        else
        {
            var leaderTeamIds = await _db.AgentTeams
                .Where(at => at.AgentId == leaderId)
                .Select(at => at.TeamId)
                .ToListAsync();
            teams = await _db.Teams
                .Where(t => leaderTeamIds.Contains(t.Id))
                .OrderBy(t => t.Name)
                .ToListAsync();
        }
        return new SelectList(teams, "Id", "Name");
    }

    private async Task<List<(string Id, string Name)>> GetAgentSelectListAsync(string leaderId)
    {
        IQueryable<AppUser> query;
        if (User.IsInRole(Roles.BrandManager))
        {
            query = _db.Users;
        }
        else
        {
            var teamIds = await _db.AgentTeams
                .Where(at => at.AgentId == leaderId).Select(at => at.TeamId).ToListAsync();
            var ids = await _db.AgentTeams
                .Where(at => teamIds.Contains(at.TeamId)).Select(at => at.AgentId)
                .Distinct().ToListAsync();
            var sakIds = await _db.Users.Where(u => u.IsSwissArmyKnife)
                .Select(u => u.Id).ToListAsync();
            var all = ids.Union(sakIds).Distinct().ToList();
            query = _db.Users.Where(u => all.Contains(u.Id));
        }

        var agents = await query
            .OrderBy(u => u.DisplayName ?? u.UserName)
            .Select(u => new { u.Id, Name = u.DisplayName ?? u.UserName ?? u.Id })
            .ToListAsync();

        return agents.Select(u => (u.Id, u.Name)).ToList();
    }

    private async Task<List<DateTime>> GetAvailablePeriodDatesAsync(PeriodType pt)
        => await _db.TeamReports
            .Where(r => r.PeriodType == pt)
            .Select(r => r.PeriodStart)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

    private static SelectList PeriodTypeSelectList(int? selected = null)
    {
        var items = Enum.GetValues<PeriodType>()
            .Select(p => new { Value = (int)p, Text = p.ToString() });
        return new SelectList(items, "Value", "Text", selected);
    }

    private static DateTime StartOfCurrentPeriod(PeriodType pt)
    {
        var today = DateTime.Today;
        return pt == PeriodType.Monthly
            ? new DateTime(today.Year, today.Month, 1)
            : today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
    }
}
