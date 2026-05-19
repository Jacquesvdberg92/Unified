using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.Performance;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class PerformanceController : Controller
{
    private readonly PerformanceService _svc;
    private readonly AppDbContext       _db;
    private readonly UserManager<AppUser> _users;

    public PerformanceController(
        PerformanceService svc,
        AppDbContext db,
        UserManager<AppUser> users)
    {
        _svc   = svc;
        _db    = db;
        _users = users;
    }

    // ── Agent: view my own reviews ────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> MyReviews()
    {
        var agentId = _users.GetUserId(User)!;
        var reviews = await _svc.GetReviewsForAgentAsync(agentId);
        return View(reviews);
    }

    // ── Leader/Manager: team review list ──────────────────────────────────

    [HttpGet]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> TeamReviews(string? agentId, int? teamId)
    {
        var leaderId = _users.GetUserId(User)!;

        List<PerformanceReview> reviews;
        if (!string.IsNullOrEmpty(agentId))
            reviews = await _svc.GetReviewsForAgentAsync(agentId);
        else
            reviews = await _svc.GetReviewsByLeaderAsync(leaderId);

        ViewBag.Agents    = await GetAgentSelectListAsync(leaderId);
        ViewBag.AgentId   = agentId;
        return View(reviews);
    }

    // ── Detail view ───────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var review = await _svc.GetReviewAsync(id);
        if (review is null) return NotFound();

        var currentUserId = _users.GetUserId(User)!;
        var isLeaderOrAdmin = User.IsInRole(Roles.TeamLeader) ||
                              User.IsInRole(Roles.BrandManager);

        if (!isLeaderOrAdmin && review.AgentId != currentUserId)
            return Forbid();

        return View(review);
    }

    // ── Create ────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> Create(string? agentId)
    {
        var leaderId = _users.GetUserId(User)!;
        ViewBag.Agents   = await GetAgentSelectListAsync(leaderId);
        ViewBag.AgentId  = agentId;
        ViewBag.Categories = CategorySelectList();
        return View(new PerformanceReview { ReviewDate = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> Create(
        PerformanceReview review,
        string[] refId,
        int[] category,
        int[] rating,
        string[] positive,
        string[] negative,
        bool[] actionRequired,
        string[] actionNote)
    {
        review.ReviewedByLeaderId = _users.GetUserId(User)!;

        for (int i = 0; i < refId.Length; i++)
        {
            review.Items.Add(new ReviewItem
            {
                Category       = (ReviewCategory)category[i],
                ReferenceId    = refId[i],
                Rating         = rating[i],
                Positive       = positive.ElementAtOrDefault(i),
                Negative       = negative.ElementAtOrDefault(i),
                ActionRequired = actionRequired.ElementAtOrDefault(i),
                ActionNote     = actionNote.ElementAtOrDefault(i)
            });
        }

        if (!ModelState.IsValid || !review.Items.Any())
        {
            var leaderId = _users.GetUserId(User)!;
            ViewBag.Agents     = await GetAgentSelectListAsync(leaderId);
            ViewBag.AgentId    = review.AgentId;
            ViewBag.Categories = CategorySelectList();
            ModelState.AddModelError("", "At least one review item is required.");
            return View(review);
        }

        try
        {
            await _svc.CreateReviewAsync(review);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError("", ex.Message);
            var leaderId = _users.GetUserId(User)!;
            ViewBag.Agents     = await GetAgentSelectListAsync(leaderId);
            ViewBag.Categories = CategorySelectList();
            return View(review);
        }

        return RedirectToAction(nameof(TeamReviews));
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteReviewAsync(id);
        return RedirectToAction(nameof(TeamReviews));
    }

    // ── Leaderboard ───────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    [OutputCache(PolicyName = "LeaderBoard")]
    public async Task<IActionResult> Leaderboard(int? teamId, int? category)
    {
        var cat = category.HasValue ? (ReviewCategory?)category.Value : null;
        var top = await _svc.GetTopRatedAgentsAsync(teamId, cat);

        var teams = await _db.Teams.ToListAsync();
        ViewBag.Teams      = new SelectList(teams, "Id", "Name", teamId);
        ViewBag.Categories = CategorySelectList(category);
        ViewBag.TeamId     = teamId;
        ViewBag.Category   = category;
        return View(top);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<SelectList> GetAgentSelectListAsync(string leaderId)
    {
        var teamIds = await _db.AgentTeams
            .Where(at => at.AgentId == leaderId)
            .Select(at => at.TeamId)
            .ToListAsync();

        List<string> allIds;

        if (teamIds.Any())
        {
            var agentIds = await _db.AgentTeams
                .Where(at => teamIds.Contains(at.TeamId))
                .Select(at => at.AgentId)
                .Distinct()
                .ToListAsync();

            var sakIds = await _db.Users
                .Where(u => u.IsSwissArmyKnife)
                .Select(u => u.Id)
                .ToListAsync();

            allIds = agentIds.Union(sakIds).Distinct().ToList();
        }
        else
        {
            // Leader not assigned to any team yet — show all users so the dropdown works
            allIds = await _db.Users
                .Select(u => u.Id)
                .ToListAsync();
        }

        var agents = await _db.Users
            .Where(u => allIds.Contains(u.Id))
            .OrderBy(u => u.DisplayName ?? u.UserName)
            .Select(u => new { u.Id, Name = u.DisplayName ?? u.UserName })
            .ToListAsync();

        return new SelectList(agents, "Id", "Name");
    }

    private static SelectList CategorySelectList(int? selected = null)
    {
        var items = Enum.GetValues<ReviewCategory>()
            .Select(c => new { Value = (int)c, Text = c.ToString() });
        return new SelectList(items, "Value", "Text", selected);
    }
}
