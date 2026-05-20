using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.CsLiveHelp;

namespace Unified.Services;

public class CsLiveHelpService
{
    private readonly AppDbContext _db;

    public CsLiveHelpService(AppDbContext db) => _db = db;

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
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();

    /// <summary>Returns other AMs' open requests for the same brands as this AM (read-only duplicate guard).</summary>
    public async Task<List<CsRequest>> GetOtherAmOpenRequestsAsync(string amId, int afterId = 0)
    {
        var amBrandIds = await _db.AgentBrands
            .Where(ab => ab.AgentId == amId)
            .Select(ab => ab.BrandId)
            .ToListAsync();

        return await _db.CsRequests
            .Where(r => r.AccountManagerId != amId
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
        string amId, int brandId, int requestTypeId, string? customDescription)
    {
        var req = new CsRequest
        {
            AccountManagerId  = amId,
            BrandId           = brandId,
            RequestTypeId     = requestTypeId,
            CustomDescription = customDescription?.Trim(),
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
        int id, string amId, int brandId, int requestTypeId, string? customDescription)
    {
        var req = await GetOwnOpenRequestAsync(id, amId);
        if (req is null) return false;

        req.BrandId           = brandId;
        req.RequestTypeId     = requestTypeId;
        req.CustomDescription = customDescription?.Trim();
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

    public async Task<bool> AddCommentAsync(int requestId, string amId, string body)
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
            IsSystemMessage = false
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

    // ── CS Agent: board ───────────────────────────────────────────────────

    /// <summary>Returns all non-archived requests for the CS board, newest first, paginated (top 50).</summary>
    public async Task<List<CsRequest>> GetBoardRequestsAsync(int afterId = 0)
        => await _db.CsRequests
            .Where(r => r.Id > afterId)
            .Include(r => r.Brand)
            .Include(r => r.RequestType)
            .Include(r => r.Comments.OrderBy(c => c.CreatedAt))
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();

    /// <summary>Returns the request regardless of owner — used by CS agents who may act on any card.</summary>
    public async Task<CsRequest?> GetRequestAsync(int id)
        => await _db.CsRequests
            .Include(r => r.Brand)
            .Include(r => r.RequestType)
            .Include(r => r.Comments.OrderBy(c => c.CreatedAt))
            .FirstOrDefaultAsync(r => r.Id == id);

    // ── CS Agent: status transition ────────────────────────────────────────

    public async Task<bool> UpdateStatusAsync(int id, CsRequestStatus newStatus)
    {
        var req = await _db.CsRequests.FindAsync(id);
        if (req is null) return false;

        req.Status    = newStatus;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── CS Agent: comments ─────────────────────────────────────────────────

    /// <summary>CS agents can comment on any request (not ownership-gated).</summary>
    public async Task<bool> CsAddCommentAsync(int requestId, string authorId, string body, bool isSystem = false)
    {
        var req = await _db.CsRequests.FindAsync(requestId);
        if (req is null) return false;

        _db.CsRequestComments.Add(new CsRequestComment
        {
            RequestId       = requestId,
            AuthorId        = authorId,
            Body            = body.Trim(),
            CreatedAt       = DateTime.UtcNow,
            IsSystemMessage = isSystem
        });
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}
