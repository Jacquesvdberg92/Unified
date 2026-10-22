using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;
using Unified.Models.Withdrawals;
using Unified.Services;

namespace Unified.Controllers;

/// <summary>
/// WD List (beta) — a board of client withdrawals that internal staff pick up and work through.
/// Every action posts back and redirects to the board, keeping the current filters and selection.
/// </summary>
[Authorize(Policy = "InternalOnly")]
public class WithdrawalsController : Controller
{
    private readonly WithdrawalService    _svc;
    private readonly AppDbContext         _db;
    private readonly UserManager<AppUser> _users;

    public WithdrawalsController(WithdrawalService svc, AppDbContext db, UserManager<AppUser> users)
    {
        _svc   = svc;
        _db    = db;
        _users = users;
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
        return BackToBoard(id, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(int id, string? returnUrl)
    {
        if (!await _svc.ReleaseAsync(id, _users.GetUserId(User)!))
            TempData["Error"] = "Only the person handling a withdrawal can release it.";
        return BackToBoard(id, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, WithdrawalStatus status, string? returnUrl)
    {
        if (!await _svc.SetStatusAsync(id, status, _users.GetUserId(User)!))
            return NotFound();
        return BackToBoard(id, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDocument(int id, WithdrawalDocuments document, string? returnUrl)
    {
        if (!WithdrawalService.AllDocuments.Contains(document)) return BadRequest();
        var wd = await _db.WithdrawalRequests.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
        if (wd is null) return NotFound();

        await _svc.SetMissingDocumentsAsync(id, wd.MissingDocuments ^ document, _users.GetUserId(User)!);
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
        return BackToBoard(id, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, string? body, string? returnUrl)
    {
        if (!await _svc.AddCommentAsync(id, _users.GetUserId(User)!, body))
            TempData["Error"] = "Write a comment before posting.";
        return BackToBoard(id, returnUrl);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

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
