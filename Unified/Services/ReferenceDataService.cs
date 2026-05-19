using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Unified.Data;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;

namespace Unified.Services;

/// <summary>
/// Provides cached access to infrequently changing reference data
/// (Brands, Teams, ShiftTemplates) to avoid repeated full-table reads.
/// </summary>
public class ReferenceDataService
{
    private readonly AppDbContext  _db;
    private readonly IMemoryCache  _cache;

    private static readonly TimeSpan BrandTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TeamTtl  = TimeSpan.FromMinutes(10);

    public ReferenceDataService(AppDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    public Task<List<Brand>> GetBrandsAsync() =>
        _cache.GetOrCreateAsync("ref:brands", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = BrandTtl;
            return await _db.Brands.OrderBy(b => b.Name).ToListAsync();
        })!;

    public Task<List<Team>> GetTeamsAsync() =>
        _cache.GetOrCreateAsync("ref:teams", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TeamTtl;
            return await _db.Teams.OrderBy(t => t.Name).ToListAsync();
        })!;

    /// <summary>Invalidate brand cache (call after any brand create/update/delete).</summary>
    public void InvalidateBrands() => _cache.Remove("ref:brands");

    /// <summary>Invalidate team cache (call after any team create/update/delete).</summary>
    public void InvalidateTeams() => _cache.Remove("ref:teams");
}
