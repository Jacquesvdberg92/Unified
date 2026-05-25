using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Unified.Hubs;
using Unified.Models.Identity;
using Unified.Services;


namespace Unified.Controllers;

[Authorize]
public class WorkDistributionController : Controller
{
    private readonly WorkDistributionService _svc;
    private readonly CsLiveAllocationService _csLiveHelp;
    private readonly UserManager<AppUser> _users;
    private readonly IHubContext<CsMessagingHub> _messagingHub;

    public WorkDistributionController(
        WorkDistributionService svc,
        CsLiveAllocationService csLiveHelp,
        UserManager<AppUser> users,
        IHubContext<CsMessagingHub> messagingHub)
    {
        _svc = svc;
        _csLiveHelp = csLiveHelp;
        _users = users;
        _messagingHub = messagingHub;
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
        ViewBag.CurrentUserDisplayName = (await _users.GetUserAsync(User))?.DisplayName ?? User.Identity?.Name ?? string.Empty;
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

    // GET /WorkDistribution/MentionCandidates
    [HttpGet]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader},{Roles.CSAgent},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> MentionCandidates()
    {
        if (!Request.Headers.ContainsKey("X-Requested-With")) return BadRequest();

        var csUsers = await GetCsMentionUsersAsync();

        var names = csUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.DisplayName))
            .Select(u => u.DisplayName)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        return Json(new { success = true, candidates = names });
    }

    // POST /WorkDistribution/Save
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Save(DateTime date, string body)
    {
        var userId = _users.GetUserId(User)!;
        await _svc.SaveAsync(date, body, userId);

        var mentionNames = ExtractMentionNames(body).ToList();
        if (mentionNames.Any())
        {
            var mentionedUserIds = await ResolveMentionedUserIdsAsync(mentionNames, userId);
            if (mentionedUserIds.Any())
            {
                var actor = (await _users.GetUserAsync(User))?.DisplayName ?? User.Identity?.Name ?? "A manager";
                await _messagingHub.Clients.Users(mentionedUserIds).SendAsync("WorkDistributionMentionNotification", new
                {
                    type = "mention",
                    contextType = "WorkDistribution",
                    contextId = date.ToString("yyyy-MM-dd"),
                    author = actor,
                    date = date.ToString("yyyy-MM-dd"),
                    timestamp = DateTime.UtcNow
                });
            }
        }

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

    private static IEnumerable<string> ExtractMentionNames(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var matches = System.Text.RegularExpressions.Regex.Matches(
            text,
            @"@([A-Za-z][A-Za-z0-9._\- ]{1,48})",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        return matches
            .Select(m => m.Groups[1].Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<string>> ResolveMentionedUserIdsAsync(IEnumerable<string> mentionNames, string authorUserId)
    {
        var targets = mentionNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!targets.Any()) return [];

        var users = await GetCsMentionUsersAsync();

        return users
            .Where(u => u.Id != authorUserId)
            .Where(u => !string.IsNullOrWhiteSpace(u.DisplayName) && targets.Contains(u.DisplayName!))
            .Select(u => u.Id)
            .Distinct()
            .ToList();
    }

    private async Task<List<AppUser>> GetCsMentionUsersAsync()
    {
        var amUsers      = await _users.GetUsersInRoleAsync(Roles.AccountManager);
        var bmUsers      = await _users.GetUsersInRoleAsync(Roles.BrandManager);
        var tlUsers      = await _users.GetUsersInRoleAsync(Roles.TeamLeader);
        var csUsers      = await _users.GetUsersInRoleAsync(Roles.CSAgent);
        var sakUsers     = await _users.GetUsersInRoleAsync(Roles.SwissArmyKnife);

        var excludedIds  = amUsers.Select(u => u.Id).ToHashSet();

        return bmUsers
            .Concat(tlUsers)
            .Concat(csUsers)
            .Concat(sakUsers)
            .Where(u => !u.IsExternal)
            .Where(u => !excludedIds.Contains(u.Id))
            .DistinctBy(u => u.Id)
            .OrderBy(u => u.DisplayName)
            .ToList();
    }
}
