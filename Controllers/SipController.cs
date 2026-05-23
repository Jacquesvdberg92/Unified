using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.Sip;

namespace Unified.Controllers;

[Authorize(Policy = "InternalOnly")]
public class SipController : Controller
{
    private static readonly HashSet<string> AllowedScreenshotExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private const long MaxScreenshotBytes = 5 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IWebHostEnvironment _env;

    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _voteAttempts = new();

    public SipController(AppDbContext db, UserManager<AppUser> users, IWebHostEnvironment env)
    {
        _db = db;
        _users = users;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string sort = "newest", SipCategory? category = null, int page = 1, int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.SipItems
            .AsNoTracking()
            .Include(s => s.Author)
            .Include(s => s.Votes)
            .AsQueryable();

        if (category.HasValue)
            query = query.Where(s => s.Category == category.Value);

        query = sort?.ToLowerInvariant() switch
        {
            "votes" => query.OrderByDescending(s => s.Votes.Count(v => v.IsUpvote) - s.Votes.Count(v => !v.IsUpvote)).ThenByDescending(s => s.CreatedAt),
            "status" => query.OrderBy(s => s.Status).ThenByDescending(s => s.CreatedAt),
            _ => query.OrderByDescending(s => s.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SipListRowViewModel
            {
                Id = s.Id,
                Title = s.Title,
                Category = s.Category,
                Status = s.Status,
                Upvotes = s.Votes.Count(v => v.IsUpvote),
                Downvotes = s.Votes.Count(v => !v.IsUpvote),
                NetScore = s.Votes.Count(v => v.IsUpvote) - s.Votes.Count(v => !v.IsUpvote),
                AuthorName = s.Author != null ? s.Author.DisplayName : s.AuthorId,
                CreatedAt = s.CreatedAt,
                OwnerNote = s.OwnerNote,
                ScreenshotPath = s.ScreenshotPath
            })
            .ToListAsync();

        var model = new SipIndexViewModel
        {
            Sort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort,
            Category = category,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Rows = rows
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View(new SipCreateInputModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SipCreateInputModel input, IFormFile? screenshot)
    {
        var screenshotPath = await TrySaveScreenshotAsync(screenshot);
        if (!ModelState.IsValid)
            return View(input);

        var userId = _users.GetUserId(User)!;

        var sip = new Sip
        {
            Title = input.Title.Trim(),
            Description = input.Description.Trim(),
            Category = input.Category,
            Status = SipStatus.Open,
            AuthorId = userId,
            ScreenshotPath = screenshotPath,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SipItems.Add(sip);
        await _db.SaveChangesAsync();

        TempData["Success"] = "SIP submitted successfully.";
        return RedirectToAction(nameof(Details), new { id = sip.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _users.GetUserId(User)!;

        var sip = await _db.SipItems
            .AsNoTracking()
            .Include(s => s.Author)
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sip is null)
            return NotFound();

        var userVote = sip.Votes.FirstOrDefault(v => v.UserId == userId);

        var model = new SipDetailsViewModel
        {
            Id = sip.Id,
            Title = sip.Title,
            Description = sip.Description,
            Category = sip.Category,
            Status = sip.Status,
            AuthorId = sip.AuthorId,
            AuthorName = sip.Author?.DisplayName ?? sip.AuthorId,
            CreatedAt = sip.CreatedAt,
            UpdatedAt = sip.UpdatedAt,
            OwnerNote = sip.OwnerNote,
            ScreenshotPath = sip.ScreenshotPath,
            Upvotes = sip.Votes.Count(v => v.IsUpvote),
            Downvotes = sip.Votes.Count(v => !v.IsUpvote),
            NetScore = sip.Votes.Count(v => v.IsUpvote) - sip.Votes.Count(v => !v.IsUpvote),
            CurrentUserVoteIsUpvote = userVote?.IsUpvote,
            CanEdit = sip.AuthorId == userId && sip.Status == SipStatus.Open
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _users.GetUserId(User)!;
        var sip = await _db.SipItems.FirstOrDefaultAsync(s => s.Id == id);
        if (sip is null)
            return NotFound();

        if (sip.AuthorId != userId || sip.Status != SipStatus.Open)
            return Forbid();

        var model = new SipCreateInputModel
        {
            Title = sip.Title,
            Description = sip.Description,
            Category = sip.Category
        };

        ViewBag.SipId = sip.Id;
        ViewBag.ExistingScreenshotPath = sip.ScreenshotPath;
        return View("Create", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SipCreateInputModel input, IFormFile? screenshot)
    {
        var userId = _users.GetUserId(User)!;
        var sip = await _db.SipItems.FirstOrDefaultAsync(s => s.Id == id);
        if (sip is null)
            return NotFound();

        if (sip.AuthorId != userId || sip.Status != SipStatus.Open)
            return Forbid();

        var screenshotPath = await TrySaveScreenshotAsync(screenshot);
        if (!ModelState.IsValid)
        {
            ViewBag.SipId = id;
            ViewBag.ExistingScreenshotPath = sip.ScreenshotPath;
            return View("Create", input);
        }

        sip.Title = input.Title.Trim();
        sip.Description = input.Description.Trim();
        sip.Category = input.Category;
        if (!string.IsNullOrWhiteSpace(screenshotPath))
            sip.ScreenshotPath = screenshotPath;
        sip.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "SIP updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _users.GetUserId(User)!;
        var sip = await _db.SipItems.FirstOrDefaultAsync(s => s.Id == id);
        if (sip is null)
            return NotFound();

        var isManager = User.IsInRole(Roles.BrandManager) || User.IsInRole(Roles.TeamLeader);
        if (!isManager)
        {
            if (sip.AuthorId != userId || sip.Status != SipStatus.Open)
                return Forbid();
        }

        _db.SipItems.Remove(sip);
        await _db.SaveChangesAsync();

        TempData["Success"] = "SIP deleted.";
        return RedirectToAction(isManager ? nameof(Admin) : nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(int id, [FromBody] SipVoteRequest request)
    {
        var userId = _users.GetUserId(User)!;

        if (!TrackVoteAttempt(userId))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, error = "Too many votes. Please wait a minute and try again." });

        var sip = await _db.SipItems
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sip is null)
            return NotFound(new { success = false, error = "SIP not found." });

        if (sip.AuthorId == userId)
            return BadRequest(new { success = false, error = "You cannot vote on your own SIP." });

        var existing = sip.Votes.FirstOrDefault(v => v.UserId == userId);

        if (existing is null)
        {
            _db.SipVotes.Add(new SipVote
            {
                SipId = sip.Id,
                UserId = userId,
                IsUpvote = request.IsUpvote,
                CastAt = DateTime.UtcNow
            });
        }
        else if (existing.IsUpvote == request.IsUpvote)
        {
            _db.SipVotes.Remove(existing);
        }
        else
        {
            existing.IsUpvote = request.IsUpvote;
            existing.CastAt = DateTime.UtcNow;
        }

        sip.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var votes = await _db.SipVotes
            .Where(v => v.SipId == id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Upvotes = g.Count(v => v.IsUpvote),
                Downvotes = g.Count(v => !v.IsUpvote)
            })
            .FirstOrDefaultAsync();

        var upvotes = votes?.Upvotes ?? 0;
        var downvotes = votes?.Downvotes ?? 0;

        return Json(new
        {
            success = true,
            upvotes,
            downvotes,
            netScore = upvotes - downvotes
        });
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Admin(SipStatus? status = null, int page = 1, int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.SipItems
            .AsNoTracking()
            .Include(s => s.Author)
            .Include(s => s.Votes)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        query = query
            .OrderByDescending(s => s.Votes.Count(v => v.IsUpvote) - s.Votes.Count(v => !v.IsUpvote))
            .ThenByDescending(s => s.CreatedAt);

        var totalCount = await query.CountAsync();

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SipListRowViewModel
            {
                Id = s.Id,
                Title = s.Title,
                Category = s.Category,
                Status = s.Status,
                Upvotes = s.Votes.Count(v => v.IsUpvote),
                Downvotes = s.Votes.Count(v => !v.IsUpvote),
                NetScore = s.Votes.Count(v => v.IsUpvote) - s.Votes.Count(v => !v.IsUpvote),
                AuthorName = s.Author != null ? s.Author.DisplayName : s.AuthorId,
                CreatedAt = s.CreatedAt,
                OwnerNote = s.OwnerNote,
                ScreenshotPath = s.ScreenshotPath
            })
            .ToListAsync();

        ViewBag.Statuses = new SelectList(Enum.GetValues<SipStatus>());

        var model = new SipAdminViewModel
        {
            StatusFilter = status,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Rows = rows
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> UpdateStatus(int id, SipStatus status, string? ownerNote)
    {
        var sip = await _db.SipItems.FirstOrDefaultAsync(s => s.Id == id);
        if (sip is null)
            return NotFound();

        sip.Status = status;
        sip.OwnerNote = string.IsNullOrWhiteSpace(ownerNote) ? null : ownerNote.Trim();
        sip.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "SIP status updated.";
        return RedirectToAction(nameof(Admin), new { status });
    }

    private async Task<string?> TrySaveScreenshotAsync(IFormFile? screenshot)
    {
        if (screenshot is null || screenshot.Length == 0)
            return null;

        var ext = Path.GetExtension(screenshot.FileName).ToLowerInvariant();
        if (!AllowedScreenshotExtensions.Contains(ext))
        {
            ModelState.AddModelError("screenshot", "Only jpg, jpeg, png, gif, and webp screenshots are allowed.");
            return null;
        }

        if (screenshot.Length > MaxScreenshotBytes)
        {
            ModelState.AddModelError("screenshot", "Screenshot must be 5 MB or smaller.");
            return null;
        }

        var folder = Path.Combine(_env.WebRootPath, "uploads", "sip");
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(folder, fileName);

        await using var fs = System.IO.File.Create(filePath);
        await screenshot.CopyToAsync(fs);

        return $"/uploads/sip/{fileName}";
    }

    private static bool TrackVoteAttempt(string userId)
    {
        var now = DateTime.UtcNow;
        var queue = _voteAttempts.GetOrAdd(userId, _ => new Queue<DateTime>());

        lock (queue)
        {
            while (queue.Count > 0 && (now - queue.Peek()).TotalSeconds > 60)
                queue.Dequeue();

            if (queue.Count >= 20)
                return false;

            queue.Enqueue(now);
            return true;
        }
    }
}
