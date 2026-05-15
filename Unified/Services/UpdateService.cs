using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Updates;

namespace Unified.Services;

public class UpdateService
{
    private readonly AppDbContext _db;

    public UpdateService(AppDbContext db)
    {
        _db = db;
    }

    // ── Read ──────────────────────────────────────────────────────────────

    public async Task<List<Update>> GetFeedAsync(int? brandId, string? tag, string? searchText, bool includeArchived = false)
    {
        var query = _db.Updates
            .Include(u => u.Author)
            .Include(u => u.AffectedBrands).ThenInclude(ub => ub.Brand)
            .AsQueryable();

        if (!includeArchived)
            query = query.Where(u => !u.IsArchived);

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
            .ToListAsync();
    }

    public async Task<Update?> GetByIdAsync(int id)
        => await _db.Updates
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
        return update;
    }

    public async Task<Update> EditUpdateAsync(Update update, IEnumerable<int> brandIds)
    {
        update.UpdatedAt = DateTime.UtcNow;
        _db.Updates.Update(update);
        await _db.SaveChangesAsync();
        await SetBrandsAsync(update.Id, brandIds);
        return update;
    }

    public async Task ArchiveAsync(int id)
    {
        var update = await _db.Updates.FindAsync(id);
        if (update is null) return;
        update.IsArchived = true;
        update.UpdatedAt  = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<int> ArchiveOldUpdatesAsync(int days)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var old = await _db.Updates
            .Where(u => !u.IsArchived && u.CreatedAt < cutoff)
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
        var update = await _db.Updates.FindAsync(id);
        if (update is null) return;
        _db.Updates.Remove(update);
        await _db.SaveChangesAsync();
    }

    // ── Pinned since last login ───────────────────────────────────────────

    public async Task<int> CountPinnedSinceAsync(DateTime? since)
    {
        var query = _db.Updates.Where(u => u.IsPinned && !u.IsArchived);
        if (since.HasValue)
            query = query.Where(u => u.CreatedAt > since.Value);
        return await query.CountAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task SetBrandsAsync(int updateId, IEnumerable<int> brandIds)
    {
        var existing = await _db.UpdateBrands.Where(ub => ub.UpdateId == updateId).ToListAsync();
        _db.UpdateBrands.RemoveRange(existing);

        foreach (var bid in brandIds.Distinct())
            _db.UpdateBrands.Add(new UpdateBrand { UpdateId = updateId, BrandId = bid });

        await _db.SaveChangesAsync();
    }
}
