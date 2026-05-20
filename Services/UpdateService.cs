using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Unified.Data;
using Unified.Models.Updates;

namespace Unified.Services;

public class UpdateService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    private const string PinnedCacheKey = "updates:pinned";
    private static readonly TimeSpan PinnedTtl = TimeSpan.FromMinutes(2);

    public UpdateService(AppDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    // ── Read ──────────────────────────────────────────────────────────────

    public async Task<List<Update>> GetFeedAsync(int? brandId, string? tag, string? searchText, bool includeArchived = false, int take = 50)
    {
        var query = includeArchived
            ? _db.Updates.IgnoreQueryFilters()
                .Include(u => u.Author)
                .Include(u => u.AffectedBrands).ThenInclude(ub => ub.Brand)
                .AsQueryable()
            : _db.Updates
                .Include(u => u.Author)
                .Include(u => u.AffectedBrands).ThenInclude(ub => ub.Brand)
                .AsQueryable();

        if (brandId.HasValue)
            query = query.Where(u =>
                !u.AffectedBrands.Any() ||
                u.AffectedBrands.Any(ub => ub.BrandId == brandId.Value));

        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(u => u.TagsJson.Contains(tag));

        if (!string.IsNullOrWhiteSpace(searchText))
            query = query.Where(u =>
                u.Title.Contains(searchText) ||
                u.Body.Contains(searchText));

        // Pinned first, then newest
        return await query
            .OrderByDescending(u => u.IsPinned)
            .ThenByDescending(u => u.CreatedAt)
            .Take(take)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<Update?> GetByIdAsync(int id)
        => await _db.Updates
            .IgnoreQueryFilters()
            .Include(u => u.Author)
            .Include(u => u.AffectedBrands).ThenInclude(ub => ub.Brand)
            .FirstOrDefaultAsync(u => u.Id == id);

    // ── Write ─────────────────────────────────────────────────────────────

    public async Task<Update> PostUpdateAsync(Update update, IEnumerable<int> brandIds)
    {
        update.CreatedAt = DateTime.UtcNow;
        update.UpdatedAt = DateTime.UtcNow;
        _db.Updates.Add(update);
        await _db.SaveChangesAsync();
        await SetBrandsAsync(update.Id, brandIds);
        InvalidatePinnedCache();
        return update;
    }

    public async Task<Update> EditUpdateAsync(Update update, IEnumerable<int> brandIds)
    {
        update.UpdatedAt = DateTime.UtcNow;
        _db.Updates.Update(update);
        await _db.SaveChangesAsync();
        await SetBrandsAsync(update.Id, brandIds);
        InvalidatePinnedCache();
        return update;
    }

    public async Task ArchiveAsync(int id)
    {
        var update = await _db.Updates.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (update is null) return;
        update.IsArchived = true;
        update.UpdatedAt  = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        InvalidatePinnedCache();
    }

    public async Task<int> ArchiveOldUpdatesAsync(int days)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var old = await _db.Updates
            .Where(u => u.CreatedAt < cutoff)
            .ToListAsync();
        foreach (var u in old)
        {
            u.IsArchived = true;
            u.UpdatedAt  = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return old.Count;
    }

    public async Task DeleteAsync(int id)
    {
        var update = await _db.Updates.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (update is null) return;
        _db.Updates.Remove(update);
        await _db.SaveChangesAsync();
        InvalidatePinnedCache();
    }

    // ── Pinned since last login ───────────────────────────────────────────

    public async Task<int> CountPinnedSinceAsync(DateTime? since)
    {
        var query = _db.Updates.Where(u => u.IsPinned);
        if (since.HasValue)
            query = query.Where(u => u.CreatedAt > since.Value);
        return await query.CountAsync();
    }

    /// <summary>Returns pinned non-archived updates, cached for 2 minutes.</summary>
    public Task<List<Update>> GetPinnedAsync() =>
        _cache.GetOrCreateAsync(PinnedCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = PinnedTtl;
            return await _db.Updates
                .Where(u => u.IsPinned)
                .Include(u => u.Author)
                .Include(u => u.AffectedBrands).ThenInclude(ub => ub.Brand)
                .OrderByDescending(u => u.CreatedAt)
                .Take(20)
                .ToListAsync();
        })!;

    private void InvalidatePinnedCache() => _cache.Remove(PinnedCacheKey);

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task SetBrandsAsync(int updateId, IEnumerable<int> brandIds)
    {
        var existing = await _db.UpdateBrands.IgnoreQueryFilters().Where(ub => ub.UpdateId == updateId).ToListAsync();
        _db.UpdateBrands.RemoveRange(existing);

        foreach (var bid in brandIds.Distinct())
            _db.UpdateBrands.Add(new UpdateBrand { UpdateId = updateId, BrandId = bid });

        await _db.SaveChangesAsync();
    }
}
