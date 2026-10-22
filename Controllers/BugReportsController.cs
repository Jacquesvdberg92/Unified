using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.BugReports;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class BugReportsController : Controller
{
    private const string ManagerRoles = $"{Roles.BrandManager},{Roles.TeamLeader}";

    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly BugReportService _bugReports;
    private readonly GitHubIssueService _github;
    private readonly IWebHostEnvironment _env;

    public BugReportsController(AppDbContext db, UserManager<AppUser> users, BugReportService bugReports,
        GitHubIssueService github, IWebHostEnvironment env)
    {
        _db = db;
        _users = users;
        _bugReports = bugReports;
        _github = github;
        _env = env;
    }

    private bool IsManager => User.IsInRole(Roles.BrandManager) || User.IsInRole(Roles.TeamLeader);

    [HttpGet]
    public async Task<IActionResult> Index(bool all = false)
    {
        var userId = _users.GetUserId(User)!;
        var showAll = all && IsManager;

        var query = _db.BugReports.AsNoTracking();
        if (!showAll)
            query = query.Where(r => r.ReporterId == userId);

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .Select(r => new BugReportRowViewModel
            {
                Id = r.Id,
                Title = r.Title,
                ReporterName = r.Reporter != null ? r.Reporter.DisplayName : r.ReporterId,
                Status = r.Status,
                GitHubIssueNumber = r.GitHubIssueNumber,
                GitHubIssueUrl = r.GitHubIssueUrl,
                LastError = r.LastError,
                SendAttempts = r.SendAttempts,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return View(new BugReportIndexViewModel
        {
            ShowAll = showAll,
            CanManage = IsManager,
            GitHubConfigured = _github.IsConfigured,
            Rows = rows
        });
    }

    [HttpGet]
    public IActionResult Create(string? from = null)
    {
        return View(new BugReportInputModel { PageUrl = ToAbsolutePageUrl(from) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Create(BugReportInputModel input, IFormFile? screenshot)
    {
        var userId = _users.GetUserId(User)!;

        string? extension = null;
        if (screenshot is { Length: > 0 })
        {
            extension = Path.GetExtension(screenshot.FileName).ToLowerInvariant();
            if (!BugReportService.AllowedScreenshotExtensions.Contains(extension))
                ModelState.AddModelError("screenshot", "Only jpg, jpeg, png, gif, and webp screenshots are allowed.");
            else if (screenshot.Length > BugReportService.MaxScreenshotBytes)
                ModelState.AddModelError("screenshot", "Screenshot must be 5 MB or smaller.");
        }

        if (ModelState.IsValid && await _bugReports.IsOverSubmitLimitAsync(userId))
            ModelState.AddModelError(string.Empty, "You've sent several reports in the last few minutes. Please wait a bit and try again.");

        if (!ModelState.IsValid)
            return View(input);

        var user = await _users.GetUserAsync(User);
        var roles = user is null ? [] : (await _users.GetRolesAsync(user)).ToList();

        var context = new BugReportContext(
            ReporterName: user?.DisplayName ?? User.Identity?.Name ?? userId,
            ReporterRoles: roles,
            PageUrl: ToAbsolutePageUrl(input.PageUrl),
            UserAgent: Request.Headers.UserAgent.ToString(),
            EnvironmentName: _env.EnvironmentName,
            SubmittedAtUtc: DateTime.UtcNow,
            ClientInfoJson: input.ClientInfo);

        var report = new BugReport
        {
            Title = input.Title.Trim(),
            Description = input.Description.Trim(),
            StepsToReproduce = string.IsNullOrWhiteSpace(input.StepsToReproduce) ? null : input.StepsToReproduce.Trim(),
            ReporterId = userId
        };

        await using (var stream = extension is null ? null : screenshot!.OpenReadStream())
            await _bugReports.SubmitAsync(report, context, stream, extension);

        if (report.Status == BugReportStatus.Sent)
            TempData["Success"] = $"Thanks! Your report was logged as issue #{report.GitHubIssueNumber}.";
        else
            TempData["Success"] = "Thanks! Your report was saved and will be sent to the team shortly.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = ManagerRoles)]
    public async Task<IActionResult> Retry(int id)
    {
        var report = await _db.BugReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report is null)
            return NotFound();

        if (await _bugReports.TrySendAsync(report))
            TempData["Success"] = $"Report sent as issue #{report.GitHubIssueNumber}.";
        else
            TempData["Error"] = report.LastError ?? "The report could not be sent.";

        return RedirectToAction(nameof(Index), new { all = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = ManagerRoles)]
    public async Task<IActionResult> Delete(int id)
    {
        var report = await _db.BugReports.FirstOrDefaultAsync(r => r.Id == id);
        if (report is null)
            return NotFound();

        if (report.Status != BugReportStatus.Pending)
            return BadRequest();

        _db.BugReports.Remove(report);
        await _db.SaveChangesAsync();
        _bugReports.DeleteScreenshotFile(report);

        TempData["Success"] = "Pending report deleted.";
        return RedirectToAction(nameof(Index), new { all = true });
    }

    // Only local paths are accepted, so the report can't be pointed at another site.
    private string? ToAbsolutePageUrl(string? from)
    {
        if (string.IsNullOrWhiteSpace(from))
            return null;

        if (Uri.TryCreate(from, UriKind.Absolute, out var absolute))
        {
            if (!string.Equals(absolute.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
                return null;
            from = absolute.PathAndQuery;
        }

        if (!Url.IsLocalUrl(from))
            return null;

        return $"{Request.Scheme}://{Request.Host}{Request.PathBase}{from}";
    }
}
