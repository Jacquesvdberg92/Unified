using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.Performance;

namespace Unified.Services;

public class PerformanceService
{
    private readonly AppDbContext _db;

    public PerformanceService(AppDbContext db)
    {
        _db = db;
    }

    // ── Read ──────────────────────────────────────────────────────────────

    public async Task<List<PerformanceReview>> GetReviewsForAgentAsync(
        string agentId, DateTime? from = null, DateTime? to = null)
    {
        var query = _db.PerformanceReviews
            .Include(r => r.ReviewedByLeader)
            .Include(r => r.Items)
            .Where(r => r.AgentId == agentId);

        if (from.HasValue) query = query.Where(r => r.ReviewDate >= from.Value);
        if (to.HasValue)   query = query.Where(r => r.ReviewDate <= to.Value);

        return await query.OrderByDescending(r => r.ReviewDate).ToListAsync();
    }

    public async Task<List<PerformanceReview>> GetReviewsByLeaderAsync(string leaderId)
        => await _db.PerformanceReviews
            .Include(r => r.Agent)
            .Include(r => r.Items)
            .Where(r => r.ReviewedByLeaderId == leaderId)
            .OrderByDescending(r => r.ReviewDate)
            .ToListAsync();

    public async Task<PerformanceReview?> GetReviewAsync(int id)
        => await _db.PerformanceReviews
            .Include(r => r.Agent)
            .Include(r => r.ReviewedByLeader)
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id);

    // ── Write ─────────────────────────────────────────────────────────────

    /// <summary>Validates ratings 1–10, then saves header + items.</summary>
    public async Task<PerformanceReview> CreateReviewAsync(PerformanceReview review)
    {
        ValidateRatings(review.Items);

        review.CreatedAt = DateTime.UtcNow;
        _db.PerformanceReviews.Add(review);
        await _db.SaveChangesAsync();
        return review;
    }

    public async Task DeleteReviewAsync(int id)
    {
        var review = await _db.PerformanceReviews.FindAsync(id);
        if (review is null) return;
        _db.PerformanceReviews.Remove(review);
        await _db.SaveChangesAsync();
    }

    // ── Aggregates ────────────────────────────────────────────────────────

    public async Task<double?> GetAverageRatingAsync(
        string agentId, ReviewCategory? category = null,
        DateTime? from = null, DateTime? to = null)
    {
        var query = _db.ReviewItems
            .Include(i => i.Review)
            .Where(i => i.Review!.AgentId == agentId);

        if (category.HasValue) query = query.Where(i => i.Category == category.Value);
        if (from.HasValue)     query = query.Where(i => i.Review!.ReviewDate >= from.Value);
        if (to.HasValue)       query = query.Where(i => i.Review!.ReviewDate <= to.Value);

        if (!await query.AnyAsync()) return null;
        return await query.AverageAsync(i => (double)i.Rating);
    }

    /// <summary>
    /// Returns agents ordered by average rating descending, optionally scoped to a team
    /// and/or a review category.  SAK agents are included in all team queries.
    /// </summary>
    public async Task<List<(AppUser Agent, double AvgRating)>> GetTopRatedAgentsAsync(
        int? teamId = null, ReviewCategory? category = null)
    {
        List<string>? agentIds = null;

        if (teamId.HasValue)
        {
            var teamMembers = await _db.AgentTeams
                .Where(at => at.TeamId == teamId.Value)
                .Select(at => at.AgentId)
                .ToListAsync();

            var sakIds = await _db.Users
                .Where(u => u.IsSwissArmyKnife)
                .Select(u => u.Id)
                .ToListAsync();

            agentIds = teamMembers.Union(sakIds).Distinct().ToList();
        }

        var itemQuery = _db.ReviewItems
            .Include(i => i.Review)
            .AsQueryable();

        if (agentIds is not null)
            itemQuery = itemQuery.Where(i => agentIds.Contains(i.Review!.AgentId));

        if (category.HasValue)
            itemQuery = itemQuery.Where(i => i.Category == category.Value);

        var grouped = await itemQuery
            .GroupBy(i => i.Review!.AgentId)
            .Select(g => new { AgentId = g.Key, Avg = g.Average(i => (double)i.Rating) })
            .OrderByDescending(x => x.Avg)
            .ToListAsync();

        var userIds = grouped.Select(g => g.AgentId).ToList();
        var users   = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        return grouped
            .Join(users, g => g.AgentId, u => u.Id, (g, u) => (u, g.Avg))
            .ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void ValidateRatings(IEnumerable<ReviewItem> items)
    {
        foreach (var item in items)
        {
            if (item.Rating < 1 || item.Rating > 10)
                throw new ArgumentOutOfRangeException(
                    nameof(item.Rating),
                    $"Rating must be between 1 and 10 (got {item.Rating} for {item.Category} '{item.ReferenceId}').");
        }
    }
}
