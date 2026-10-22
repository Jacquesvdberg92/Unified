using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.Withdrawals;

namespace Unified.Services;

/// <summary>
/// WD List (beta) — tracks client withdrawals, who is handling them, missing documents and comments.
/// Every change of owner, status or documents is written to the comment thread as a system line.
/// </summary>
public class WithdrawalService
{
    /// <summary>Done withdrawals stay on the board for this long, then drop off (they remain searchable).</summary>
    public static readonly TimeSpan DoneVisibleFor = TimeSpan.FromDays(14);

    public const int MaxCommentLength = 2000;
    public const int MaxRelatedLength = 2000;

    public static readonly WithdrawalDocuments[] AllDocuments =
    [
        WithdrawalDocuments.POI, WithdrawalDocuments.POR, WithdrawalDocuments.POD,
        WithdrawalDocuments.CC,  WithdrawalDocuments.BD,  WithdrawalDocuments.CRP
    ];

    public enum Scope { All, Mine, Unassigned }

    private readonly AppDbContext _db;

    public WithdrawalService(AppDbContext db) => _db = db;

    // ── Access ────────────────────────────────────────────────────────────

    /// <summary>Roles that always see the WD List. Everyone else needs <see cref="AppUser.HasWdListAccess"/>.</summary>
    public static readonly string[] AlwaysAllowedRoles = [Roles.TeamLeader, Roles.BrandManager];

    public async Task<bool> CanAccessAsync(ClaimsPrincipal user, string? userId)
    {
        if (AlwaysAllowedRoles.Any(user.IsInRole)) return true;
        if (userId is null) return false;
        return await _db.Users.AnyAsync(u => u.Id == userId && u.HasWdListAccess && !u.IsExternal);
    }

    /// <summary>Ids of everyone who can open the WD List (leader roles plus users given access).</summary>
    public async Task<List<string>> GetAccessUserIdsAsync()
    {
        var byRole = from ur in _db.UserRoles
                     join r in _db.Roles on ur.RoleId equals r.Id
                     where AlwaysAllowedRoles.Contains(r.Name!)
                     select ur.UserId;
        var byFlag = _db.Users.Where(u => u.HasWdListAccess && !u.IsExternal).Select(u => u.Id);
        return (await byRole.ToListAsync()).Union(await byFlag.ToListAsync()).Distinct().ToList();
    }

    /// <summary>People to notify about a change on a withdrawal: its owner and its creator, never the person who made the change.</summary>
    public async Task<List<string>> GetFollowerIdsAsync(int id, string actorId)
    {
        var wd = await _db.WithdrawalRequests.AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new { w.AssignedToId, w.CreatedById })
            .FirstOrDefaultAsync();
        if (wd is null) return [];
        return new[] { wd.AssignedToId, wd.CreatedById }
            .Where(u => !string.IsNullOrEmpty(u) && u != actorId)
            .Select(u => u!)
            .Distinct()
            .ToList();
    }

    // ── Queries ───────────────────────────────────────────────────────────

    public async Task<List<WithdrawalRequest>> GetBoardAsync(
        string userId, Scope scope = Scope.All, int? brandId = null, string? search = null, bool missingOnly = false)
    {
        var doneCutoff = DateTime.UtcNow - DoneVisibleFor;
        var query = _db.WithdrawalRequests
            .Include(w => w.Brand)
            .Include(w => w.AssignedTo)
            .Include(w => w.Comments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var idTerm = term.StartsWith("WD-", StringComparison.OrdinalIgnoreCase) ? term[3..] : term;
            query = int.TryParse(idTerm, out var id)
                ? query.Where(w => w.Id == id || w.ClientId.Contains(term))
                : query.Where(w => w.ClientId.Contains(term));
        }
        else
        {
            query = query.Where(w => w.Status != WithdrawalStatus.Done || w.UpdatedAt >= doneCutoff);
        }

        query = scope switch
        {
            Scope.Mine       => query.Where(w => w.AssignedToId == userId),
            Scope.Unassigned => query.Where(w => w.AssignedToId == null),
            _                => query
        };

        if (brandId is int b) query = query.Where(w => w.BrandId == b);
        if (missingOnly)      query = query.Where(w => w.MissingDocuments != WithdrawalDocuments.None);

        return await query.OrderByDescending(w => w.CreatedAt).ToListAsync();
    }

    public async Task<WithdrawalRequest?> GetAsync(int id) =>
        await _db.WithdrawalRequests
            .Include(w => w.Brand)
            .Include(w => w.AssignedTo)
            .Include(w => w.CreatedBy)
            .Include(w => w.Comments.OrderBy(c => c.CreatedAt)).ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(w => w.Id == id);

    // ── Commands ──────────────────────────────────────────────────────────

    public async Task<WithdrawalRequest> CreateAsync(
        string userId, string clientId, int brandId, decimal? amount, string? currency,
        WithdrawalDocuments missing, string? relatedTickets, string? firstComment, bool pickUp)
    {
        var now = DateTime.UtcNow;
        var wd = new WithdrawalRequest
        {
            ClientId         = clientId.Trim(),
            BrandId          = brandId,
            Amount           = amount,
            Currency         = NormaliseCurrency(currency, amount),
            MissingDocuments = missing,
            RelatedTickets   = Clean(relatedTickets, MaxRelatedLength),
            CreatedById      = userId,
            CreatedAt        = now,
            UpdatedAt        = now,
            AssignedToId     = pickUp ? userId : null,
            Status           = pickUp ? WithdrawalStatus.InProgress : WithdrawalStatus.Open
        };
        wd.Comments.Add(System(userId, "created this withdrawal", now));
        if (pickUp) wd.Comments.Add(System(userId, "picked this up", now));
        var body = Clean(firstComment, MaxCommentLength);
        if (body is not null)
            wd.Comments.Add(new WithdrawalComment { AuthorId = userId, Body = body, CreatedAt = now });

        _db.WithdrawalRequests.Add(wd);
        await _db.SaveChangesAsync();
        return wd;
    }

    /// <summary>Assigns the withdrawal to <paramref name="userId"/>. False when someone else already has it.</summary>
    public async Task<bool> PickUpAsync(int id, string userId)
    {
        var wd = await _db.WithdrawalRequests.FindAsync(id);
        if (wd is null) return false;
        if (wd.AssignedToId == userId) return true;
        if (wd.AssignedToId is not null) return false;

        wd.AssignedToId = userId;
        if (wd.Status == WithdrawalStatus.Open) wd.Status = WithdrawalStatus.InProgress;
        Touch(wd, userId, "picked this up");
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Unassigns the withdrawal. Only the current owner can release it.</summary>
    public async Task<bool> ReleaseAsync(int id, string userId)
    {
        var wd = await _db.WithdrawalRequests.FindAsync(id);
        if (wd is null || wd.AssignedToId != userId) return false;

        wd.AssignedToId = null;
        if (wd.Status == WithdrawalStatus.InProgress) wd.Status = WithdrawalStatus.Open;
        Touch(wd, userId, "released this");
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetStatusAsync(int id, WithdrawalStatus status, string userId)
    {
        if (!Enum.IsDefined(status)) return false;
        var wd = await _db.WithdrawalRequests.FindAsync(id);
        if (wd is null) return false;
        if (wd.Status == status) return true;

        wd.Status = status;
        Touch(wd, userId, $"moved this to {status.ToLabel()}");
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetMissingDocumentsAsync(int id, WithdrawalDocuments missing, string userId)
    {
        var wd = await _db.WithdrawalRequests.FindAsync(id);
        if (wd is null) return false;
        if (wd.MissingDocuments == missing) return true;

        var received = AllDocuments.Where(d => wd.MissingDocuments.HasFlag(d) && !missing.HasFlag(d)).ToList();
        var added    = AllDocuments.Where(d => !wd.MissingDocuments.HasFlag(d) && missing.HasFlag(d)).ToList();
        var parts = new List<string>();
        if (received.Count > 0) parts.Add($"marked {string.Join(", ", received)} as received");
        if (added.Count > 0)    parts.Add($"marked {string.Join(", ", added)} as missing");

        wd.MissingDocuments = missing;
        Touch(wd, userId, string.Join(" and ", parts));
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateDetailsAsync(int id, decimal? amount, string? currency, string? relatedTickets, string userId)
    {
        var wd = await _db.WithdrawalRequests.FindAsync(id);
        if (wd is null) return false;

        var newCurrency = NormaliseCurrency(currency, amount);
        var newRelated  = Clean(relatedTickets, MaxRelatedLength);
        var changes = new List<string>();
        if (wd.Amount != amount || wd.Currency != newCurrency) changes.Add("the amount");
        if (wd.RelatedTickets != newRelated)                   changes.Add("the related tickets");
        if (changes.Count == 0) return true;

        wd.Amount         = amount;
        wd.Currency       = newCurrency;
        wd.RelatedTickets = newRelated;
        Touch(wd, userId, $"updated {string.Join(" and ", changes)}");
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddCommentAsync(int id, string userId, string? body)
    {
        var text = Clean(body, MaxCommentLength);
        if (text is null) return false;
        var wd = await _db.WithdrawalRequests.FindAsync(id);
        if (wd is null) return false;

        var now = DateTime.UtcNow;
        _db.WithdrawalComments.Add(new WithdrawalComment { WithdrawalId = id, AuthorId = userId, Body = text, CreatedAt = now });
        wd.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void Touch(WithdrawalRequest wd, string userId, string activity)
    {
        var now = DateTime.UtcNow;
        wd.UpdatedAt = now;
        _db.WithdrawalComments.Add(System(userId, activity, now, wd.Id));
    }

    private static WithdrawalComment System(string userId, string body, DateTime at, int withdrawalId = 0) =>
        new() { WithdrawalId = withdrawalId, AuthorId = userId, Body = body, CreatedAt = at, IsSystemMessage = true };

    private static string? NormaliseCurrency(string? currency, decimal? amount)
    {
        if (amount is null || string.IsNullOrWhiteSpace(currency)) return null;
        var c = currency.Trim().ToUpperInvariant();
        return c.Length == 3 && c.All(char.IsAsciiLetter) ? c : null;
    }

    private static string? Clean(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        return t.Length > max ? t[..max] : t;
    }
}
