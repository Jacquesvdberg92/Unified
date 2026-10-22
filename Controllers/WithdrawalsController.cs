using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Hubs;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;
using Unified.Models.Withdrawals;
using Unified.Services;

namespace Unified.Controllers;

/// <summary>
/// WD List (beta) — a board of client withdrawals that internal staff pick up and work through.
/// Every action posts back and redirects to the board, keeping the current filters and selection.
/// Access: team leaders and brand managers always; anyone else only with AppUser.HasWdListAccess.
/// The owner and creator of a withdrawal get a live notification when someone else changes it,
/// and everyone with access is told about new withdrawals.
/// </summary>
[Authorize(Policy = "InternalOnly")]
public class WithdrawalsController : Controller
{
    private readonly WithdrawalService    _svc;
    private readonly AppDbContext         _db;
    private readonly UserManager<AppUser> _users;
    private readonly IHubContext<CsMessagingHub> _hub;

    public WithdrawalsController(WithdrawalService svc, AppDbContext db, UserManager<AppUser> users,
        IHubContext<CsMessagingHub> hub)
    {
        _svc   = svc;
        _db    = db;
        _users = users;
        _hub   = hub;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!await _svc.CanAccessAsync(User, _users.GetUserId(User)))
        {
            context.Result = Forbid();
            return;
        }
        await next();
    }

    // ── GET /Withdrawals ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(
        WithdrawalService.Scope scope = WithdrawalService.Scope.All, int? brandId = null,
        string? q = null, bool missingOnly = false, int? id = null)
    {
        var userId = _users.GetUserId(User)!;
        var items  = await _svc.GetBoardAsync(userId, scope, brandId, q, missingOnly);

        WithdrawalRequest? selected = null;
        if (id is int selId)
            selected = await _svc.GetAsync(selId);

        var vm = new WithdrawalBoardViewModel
        {
            Items       = items,
            Selected    = selected,
            Brands      = await _db.Brands.AsNoTracking().OrderBy(b => b.Name).ToListAsync(),
            CurrentUser = userId,
            Scope       = scope,
            BrandId     = brandId,
            Query       = q,
            MissingOnly = missingOnly
        };
        return View(vm);
    }

    // ── POST /Withdrawals/Create ──────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string? clientId, int brandId, string? amount, string? currency,
        WithdrawalDocuments[]? missing, string? relatedTickets, string? firstComment, bool pickUp)
    {
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Trim().Length > 100)
        {
            TempData["Error"] = "A CL ID of up to 100 characters is required.";
            return RedirectToAction(nameof(Index));
        }
        if (!await _db.Brands.AnyAsync(b => b.Id == brandId))
        {
            TempData["Error"] = "Choose a brand.";
            return RedirectToAction(nameof(Index));
        }
        if (!TryParseAmount(amount, out var parsedAmount))
        {
            TempData["Error"] = "The amount must be a positive number.";
            return RedirectToAction(nameof(Index));
        }

        var wd = await _svc.CreateAsync(_users.GetUserId(User)!, clientId, brandId, parsedAmount, currency,
            Combine(missing), relatedTickets, firstComment, pickUp);
        await NotifyAsync((await _svc.GetAccessUserIdsAsync()).Where(u => u != wd.CreatedById),
            $"New withdrawal WD-{wd.Id}", $"{await ActorNameAsync()} added CL {wd.ClientId}", wd.Id);
        TempData["Success"] = $"WD-{wd.Id} created.";
        return RedirectToAction(nameof(Index), new { id = wd.Id });
    }

    // ── POST actions on one withdrawal ────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PickUp(int id, string? returnUrl)
    {
        if (!await _svc.PickUpAsync(id, _users.GetUserId(User)!))
            TempData["Error"] = "Someone else has already picked this up.";
        else
            await NotifyFollowersAsync(id, "picked it up");
        return BackToBoard(id, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(int id, string? returnUrl)
    {
        if (!await _svc.ReleaseAsync(id, _users.GetUserId(User)!))
            TempData["Error"] = "Only the person handling a withdrawal can release it.";
        else
            await NotifyFollowersAsync(id, "released it");
        return BackToBoard(id, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, WithdrawalStatus status, string? returnUrl)
    {
        if (!await _svc.SetStatusAsync(id, status, _users.GetUserId(User)!))
            return NotFound();
        await NotifyFollowersAsync(id, $"moved it to {status.ToLabel()}");
        return BackToBoard(id, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDocument(int id, WithdrawalDocuments document, string? returnUrl)
    {
        if (!WithdrawalService.AllDocuments.Contains(document)) return BadRequest();
        var wd = await _db.WithdrawalRequests.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
        if (wd is null) return NotFound();

        var nowMissing = !wd.MissingDocuments.HasFlag(document);
        await _svc.SetMissingDocumentsAsync(id, wd.MissingDocuments ^ document, _users.GetUserId(User)!);
        await NotifyFollowersAsync(id, $"marked {document} as {(nowMissing ? "missing" : "received")}");
        return BackToBoard(id, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDetails(int id, string? amount, string? currency, string? relatedTickets, string? returnUrl)
    {
        if (!TryParseAmount(amount, out var parsedAmount))
        {
            TempData["Error"] = "The amount must be a positive number.";
            return BackToBoard(id, returnUrl);
        }
        if (!await _svc.UpdateDetailsAsync(id, parsedAmount, currency, relatedTickets, _users.GetUserId(User)!))
            return NotFound();
        await NotifyFollowersAsync(id, "updated its details");
        return BackToBoard(id, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, string? body, string? returnUrl)
    {
        if (!await _svc.AddCommentAsync(id, _users.GetUserId(User)!, body))
            TempData["Error"] = "Write a comment before posting.";
        else
            await NotifyFollowersAsync(id, "commented", Preview(body!));
        return BackToBoard(id, returnUrl);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task NotifyFollowersAsync(int id, string action, string? detail = null)
    {
        var recipients = await _svc.GetFollowerIdsAsync(id, _users.GetUserId(User)!);
        if (recipients.Count == 0) return;
        var actor = await ActorNameAsync();
        await NotifyAsync(recipients, $"WD-{id}", detail is null ? $"{actor} {action}" : $"{actor} {action}: {detail}", id);
    }

    /// <summary>Pushes a toast to each user through the site-wide live notification connection.</summary>
    private async Task NotifyAsync(IEnumerable<string> userIds, string title, string message, int id)
    {
        var ids = userIds.ToList();
        if (ids.Count == 0) return;
        await _hub.Clients.Users(ids).SendAsync("WithdrawalNotification", new
        {
            id,
            title,
            message,
            url = Url.Action(nameof(Index), new { id })
        });
    }

    private string? _actorName;

    private async Task<string> ActorNameAsync()
    {
        if (_actorName is not null) return _actorName;
        var user = await _users.GetUserAsync(User);
        _actorName = !string.IsNullOrWhiteSpace(user?.DisplayName) ? user.DisplayName : User.Identity?.Name ?? "Someone";
        return _actorName;
    }

    private static string Preview(string text)
    {
        var t = text.Trim();
        return t.Length > 80 ? t[..80] + "…" : t;
    }

    /// <summary>Returns to the board the action came from (local URLs only), else the board with this withdrawal open.</summary>
    private IActionResult BackToBoard(int id, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index), new { id });

    private static WithdrawalDocuments Combine(WithdrawalDocuments[]? docs) =>
        (docs ?? []).Where(d => WithdrawalService.AllDocuments.Contains(d))
                    .Aggregate(WithdrawalDocuments.None, (acc, d) => acc | d);

    private static bool TryParseAmount(string? input, out decimal? amount)
    {
        amount = null;
        if (string.IsNullOrWhiteSpace(input)) return true;
        // The form uses <input type="number">, which always submits an invariant decimal
        if (!decimal.TryParse(input.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            || value <= 0 || value >= 10_000_000_000m)
            return false;
        amount = Math.Round(value, 2);
        return true;
    }
}

public class WithdrawalBoardViewModel
{
    public List<WithdrawalRequest> Items    { get; set; } = [];
    public WithdrawalRequest?      Selected { get; set; }
    public List<Brand>             Brands   { get; set; } = [];
    public string                  CurrentUser { get; set; } = string.Empty;
    public WithdrawalService.Scope Scope    { get; set; }
    public int?                    BrandId  { get; set; }
    public string?                 Query    { get; set; }
    public bool                    MissingOnly { get; set; }
}
