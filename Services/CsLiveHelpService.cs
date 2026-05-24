using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.CsLiveHelp;
using Unified.Models.Identity;

namespace Unified.Services;

/// <summary>
/// CS Live Help Service — data access and mutation layer.
/// 
/// ARCHITECTURE REFERENCE: See docs/CsLiveHelp-Architecture.md for:
/// - Overall system architecture and three-page integration
/// - Data visibility rules for each page (own, escalated, internal)
/// - Comment visibility and filtering rules
/// 
/// Key patterns:
/// - GetAmRequestsAsync(amId) — returns AM's own requests only
/// - GetBoardRequestsAsync() — returns all non-internal, non-completed requests
/// - GetAllBrandsRequestsAsync() — returns internal + escalated requests (CS-only)
/// - GetRequestAsync(id) — returns single request with all comments (no filtering)
/// - UpdateStatusAsync(id, status, csId) — sets AssignedToId for drag-drop tracking
/// </summary>
public class CsLiveHelpService
{
    private readonly AppDbContext _db;

    public CsLiveHelpService(AppDbContext db) => _db = db;

    public sealed record NotificationRecipients(
        IReadOnlyCollection<string> BrandAgentIds,
        IReadOnlyCollection<string> TeamAgentIds,
        IReadOnlyCollection<string> MentionedUserIds,
        IReadOnlyCollection<string> AllUniqueAgentIds,
        int? TeamId,
        int? BrandId);

    // ── Reference data ────────────────────────────────────────────────────

    public async Task<List<CsRequestType>> GetRequestTypesAsync()
        => await _db.CsRequestTypes.OrderBy(t => t.Id).ToListAsync();

    // ── AM: own requests ──────────────────────────────────────────────────

    /// <summary>Returns the AM's own open/in-progress requests, keyset-paginated (top 50 per call).</summary>
    public async Task<List<CsRequest>> GetAmRequestsAsync(string amId, int afterId = 0)
        => await _db.CsRequests
            .Where(r => r.AccountManagerId == amId && r.Id > afterId)
            .Include(r => r.Brand)
            .Include(r => r.RequestType)
            .Include(r => r.Comments.OrderBy(c => c.CreatedAt))
                .ThenInclude(c => c.Author)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();

    /// <summary>Returns other AMs' open requests for the same brands as this AM (read-only duplicate guard).</summary>
    public async Task<List<CsRequest>> GetOtherAmOpenRequestsAsync(string amId, int afterId = 0)
    {
        // AMs are external users — they are not in AgentBrands, so derive brand IDs from their own requests
        var amBrandIds = await _db.CsRequests
            .Where(r => r.AccountManagerId == amId)
            .Select(r => r.BrandId)
            .Distinct()
            .ToListAsync();

        if (amBrandIds.Count == 0)
            return new List<CsRequest>();

        return await _db.CsRequests
            .Where(r => r.AccountManagerId != amId
                     && !r.IsInternal
                     && amBrandIds.Contains(r.BrandId)
                     && (r.Status == CsRequestStatus.Open || r.Status == CsRequestStatus.InProgress)
                     && r.Id > afterId)
            .Include(r => r.Brand)
            .Include(r => r.RequestType)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    // ── AM: CRUD ──────────────────────────────────────────────────────────

    public async Task<CsRequest> CreateRequestAsync(
        string amId, int brandId, int requestTypeId, string? customDescription, string? clientId)
    {
        var req = new CsRequest
        {
            AccountManagerId  = amId,
            BrandId           = brandId,
            RequestTypeId     = requestTypeId,
            CustomDescription = customDescription?.Trim(),
            ClientId          = clientId?.Trim(),
            Status            = CsRequestStatus.Open,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow
        };
        _db.CsRequests.Add(req);
        await _db.SaveChangesAsync();
        return req;
    }

    /// <summary>Returns null if the request doesn't belong to the AM or is not Open.</summary>
    public async Task<CsRequest?> GetOwnOpenRequestAsync(int id, string amId)
        => await _db.CsRequests
            .Include(r => r.Brand)
            .Include(r => r.RequestType)
            .FirstOrDefaultAsync(r => r.Id == id && r.AccountManagerId == amId && r.Status == CsRequestStatus.Open);

    public async Task<bool> EditRequestAsync(
        int id, string amId, int brandId, int requestTypeId, string? customDescription, string? clientId)
    {
        var req = await GetOwnOpenRequestAsync(id, amId);
        if (req is null) return false;

        req.BrandId           = brandId;
        req.RequestTypeId     = requestTypeId;
        req.CustomDescription = customDescription?.Trim();
        req.ClientId          = clientId?.Trim();
        req.UpdatedAt         = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRequestAsync(int id, string amId)
    {
        var req = await GetOwnOpenRequestAsync(id, amId);
        if (req is null) return false;

        _db.CsRequests.Remove(req);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── AM: comments ──────────────────────────────────────────────────────

    public async Task<bool> AddCommentAsync(int requestId, string amId, string body, string? imagePath = null)
    {
        // AM can only comment on their own requests
        var req = await _db.CsRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.AccountManagerId == amId);
        if (req is null) return false;

        _db.CsRequestComments.Add(new CsRequestComment
        {
            RequestId  = requestId,
            AuthorId   = amId,
            Body       = body.Trim(),
            CreatedAt  = DateTime.UtcNow,
            IsSystemMessage = false,
            IsCsInternalOnly = false,
            ImagePath  = imagePath
        });
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Audit logging ─────────────────────────────────────────────────────

    public async Task AuditAsync(string userId, string action, int? entityId, string ipAddress)
    {
        _db.AmAuditLogs.Add(new AmAuditLog
        {
            UserId    = userId,
            Action    = action,
            EntityId  = entityId,
            Timestamp = DateTime.UtcNow,
            IpAddress = ipAddress
        });
        await _db.SaveChangesAsync();
    }

    // ── Rate limit helper ─────────────────────────────────────────────────

    /// <summary>Returns true when the AM has posted more than <paramref name="maxCount"/> actions in the last minute.</summary>
    public async Task<bool> IsRateLimitedAsync(string amId, int maxCount = 30)
    {
        var since = DateTime.UtcNow.AddMinutes(-1);
        var count = await _db.AmAuditLogs
            .CountAsync(a => a.UserId == amId && a.Timestamp >= since);
        return count >= maxCount;
    }

    // ── Duplicate check ───────────────────────────────────────────────────

    /// <summary>Returns true when another open request for the same brand + type already exists for this AM.</summary>
    public async Task<bool> HasDuplicateOpenAsync(string amId, int brandId, int requestTypeId)
        => await _db.CsRequests.AnyAsync(r =>
            r.AccountManagerId == amId &&
            r.BrandId == brandId &&
            r.RequestTypeId == requestTypeId &&
            r.Status == CsRequestStatus.Open);

    // ── CS Agent: All-Brands internal board ──────────────────────────────

    /// <summary>
    /// Returns internal CS requests for the "All Brands" board.
    /// Only requests created via the internal CS flow (IsInternal = true) are included.
    /// <paramref name="escalatedOnly"/> controls the status filter.
    /// </summary>
    public async Task<List<CsRequest>> GetAllBrandsRequestsAsync(bool escalatedOnly = false, int afterId = 0)
    {
        // Include both internal requests AND escalated AM-originated requests
        var query = _db.CsRequests
            .Where(r => r.Id > afterId && (r.IsInternal || r.Status == CsRequestStatus.Escalated));

        if (escalatedOnly)
            query = query.Where(r => r.Status == CsRequestStatus.Escalated);
        else
            query = query.Where(r => r.Status != CsRequestStatus.Completed || r.Status == CsRequestStatus.Escalated);

        return await query
            .Include(r => r.Brand)
            .Include(r => r.RequestType)
            .Include(r => r.AccountManager)
            .Include(r => r.AssignedTo)
            .Include(r => r.Comments.OrderBy(c => c.CreatedAt))
                .ThenInclude(c => c.Author)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    /// <summary>Returns how many cards are currently in Escalated status (used for sidebar badge).</summary>
    public async Task<int> GetEscalatedCountAsync()
        => await _db.CsRequests.CountAsync(r => r.Status == CsRequestStatus.Escalated);

    /// <summary>Creates a CS-internal request (not originating from an AM).</summary>
    public async Task<CsRequest> CreateInternalRequestAsync(
        string authorId, int brandId, int requestTypeId, string? customDescription,
        string? clientId = null)
    {
        var req = new CsRequest
        {
            AccountManagerId  = null,
            IsInternal        = true,
            BrandId           = brandId,
            RequestTypeId     = requestTypeId,
            CustomDescription = customDescription?.Trim(),
            ClientId          = clientId?.Trim(),
            Status            = CsRequestStatus.Open,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow
        };
        _db.CsRequests.Add(req);
        await _db.SaveChangesAsync();
        return req;
    }

    /// <summary>Resolves an escalated card — sets Status to Completed and posts a system comment.
    /// Returns the resolved <see cref="CsRequest"/> (with AccountManagerId populated) or null on failure.</summary>
    public async Task<CsRequest?> ResolveEscalationAsync(int id, string authorId)
    {
        var req = await _db.CsRequests.FindAsync(id);
        if (req is null || req.Status != CsRequestStatus.Escalated) return null;

        req.Status    = CsRequestStatus.Completed;
        req.UpdatedAt = DateTime.UtcNow;
        _db.CsRequestComments.Add(new CsRequestComment
        {
            RequestId       = id,
            AuthorId        = authorId,
            Body            = "Escalation resolved.",
            CreatedAt       = DateTime.UtcNow,
            IsSystemMessage = true
        });
        await _db.SaveChangesAsync();
        return req;
    }

    // ── CS Agent: board ───────────────────────────────────────────────────

    /// <summary>Returns all non-archived requests for the CS board, newest first, paginated (top 50).
    /// Internal-only requests (posted from RequestsAllBrands) are excluded — they live on that board only.</summary>
    public async Task<List<CsRequest>> GetBoardRequestsAsync(int afterId = 0, CsRequestStatus? status = null)
    {
        var q = _db.CsRequests.Where(r => r.Id > afterId && !r.IsInternal);
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        return await q
            .Include(r => r.Brand)
            .Include(r => r.RequestType)
            .Include(r => r.AssignedTo)
            .Include(r => r.Comments.OrderBy(c => c.CreatedAt))
                .ThenInclude(c => c.Author)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    /// <summary>Returns the request regardless of owner — used by CS agents who may act on any card.</summary>
    public async Task<CsRequest?> GetRequestAsync(int id)
        => await _db.CsRequests
            .Include(r => r.Brand)
            .Include(r => r.RequestType)
            .Include(r => r.AssignedTo)
            .Include(r => r.Comments.OrderBy(c => c.CreatedAt))
                .ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(r => r.Id == id);

    // ── CS Agent: status transition ────────────────────────────────────────

    /// <summary>
    /// Updates the card status and optionally assigns the card to the agent who moved it.
    /// Pass <paramref name="assignedToId"/> to record the mover; null leaves existing assignment unchanged.
    /// </summary>
    public async Task<bool> UpdateStatusAsync(int id, CsRequestStatus newStatus, string? assignedToId = null)
    {
        var req = await _db.CsRequests.FindAsync(id);
        if (req is null) return false;

        req.Status    = newStatus;
        req.UpdatedAt = DateTime.UtcNow;
        if (assignedToId is not null)
            req.AssignedToId = assignedToId;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── CS Agent: comments ─────────────────────────────────────────────────

    /// <summary>CS agents can comment on any request (not ownership-gated).</summary>
    public async Task<bool> CsAddCommentAsync(int requestId, string authorId, string body, bool isSystem = false, bool isCsInternalOnly = false, string? imagePath = null)
    {
        var req = await _db.CsRequests.FindAsync(requestId);
        if (req is null) return false;

        _db.CsRequestComments.Add(new CsRequestComment
        {
            RequestId       = requestId,
            AuthorId        = authorId,
            Body            = body.Trim(),
            CreatedAt       = DateTime.UtcNow,
            IsSystemMessage = isSystem,
            IsCsInternalOnly = isCsInternalOnly,
            ImagePath       = imagePath
        });
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int?> ResolveTeamIdForRequestAsync(int requestId)
    {
        var req = await _db.CsRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        if (req is null) return null;

        var teamMention = await _db.CsRequestComments
            .Where(c => c.RequestId == requestId && c.IsSystemMessage && c.Body.StartsWith("Team allocation:"))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.Body)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(teamMention)) return null;

        var teamName = teamMention
            .Replace("Team allocation:", string.Empty)
            .Replace(".", string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(teamName)) return null;

        var teamId = await _db.Teams
            .Where(t => t.Name == teamName)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync();

        return teamId;
    }

    public async Task<NotificationRecipients> ResolveRecipientsAsync(int requestId, IEnumerable<string>? mentionNames)
    {
        var req = await _db.CsRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (req is null)
        {
            return new NotificationRecipients([], [], [], [], null, null);
        }

        var brandAgentIds = await _db.AgentBrands
            .Where(ab => ab.BrandId == req.BrandId)
            .Select(ab => ab.AgentId)
            .Distinct()
            .ToListAsync();

        var teamId = await ResolveTeamIdForRequestAsync(requestId);
        var teamAgentIds = teamId.HasValue
            ? await _db.AgentTeams
                .Where(at => at.TeamId == teamId.Value)
                .Select(at => at.AgentId)
                .Distinct()
                .ToListAsync()
            : new List<string>();

        var normalizedMentionNames = (mentionNames ?? Enumerable.Empty<string>())
            .Select(n => (n ?? string.Empty).Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mentionedUserIds = normalizedMentionNames.Count == 0
            ? new List<string>()
            : await _db.Users
                .Where(u => normalizedMentionNames.Contains(u.DisplayName))
                .Select(u => u.Id)
                .Distinct()
                .ToListAsync();

        var all = brandAgentIds
            .Concat(teamAgentIds)
            .Concat(mentionedUserIds)
            .Distinct()
            .ToList();

        return new NotificationRecipients(
            brandAgentIds,
            teamAgentIds,
            mentionedUserIds,
            all,
            teamId,
            req.BrandId);
    }

    public IEnumerable<string> ExtractMentionNames(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Enumerable.Empty<string>();

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

    public async Task<IReadOnlyCollection<string>> GetMentionCandidatesAsync(int requestId)
    {
        var req = await _db.CsRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (req is null) return [];

        var recipients = await ResolveRecipientsAsync(requestId, null);

        var candidateUserIds = recipients.AllUniqueAgentIds
            .Concat(string.IsNullOrWhiteSpace(req.AccountManagerId) ? [] : [req.AccountManagerId])
            .Concat(string.IsNullOrWhiteSpace(req.AssignedToId) ? [] : [req.AssignedToId])
            .Distinct()
            .ToList();

        if (!candidateUserIds.Any()) return [];

        var names = await _db.Users
            .Where(u => candidateUserIds.Contains(u.Id))
            .Select(u => u.DisplayName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        return names;
    }
}
