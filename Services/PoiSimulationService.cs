using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Poi;

namespace Unified.Services;

public class PoiSimulationService
{
    private readonly AppDbContext _db;

    public PoiSimulationService(AppDbContext db) => _db = db;

    // ── Log a new simulation ──────────────────────────────────────────────

    public async Task<PoiSimulation> LogSimulationAsync(
        string clientId, int brandId, string loggedById, string notes)
    {
        var sim = new PoiSimulation
        {
            ClientId    = clientId.Trim(),
            BrandId     = brandId,
            LoggedById  = loggedById,
            Notes       = notes,
            SimulatedAt = DateTime.UtcNow
        };
        _db.PoiSimulations.Add(sim);
        await _db.SaveChangesAsync();
        return sim;
    }

    // ── Mark POI as received ──────────────────────────────────────────────

    public async Task<bool> MarkReceivedAsync(int id, string userId)
    {
        var sim = await _db.PoiSimulations.FindAsync(id);
        if (sim is null || sim.PoiReceived) return false;

        sim.PoiReceived   = true;
        sim.ReceivedAt    = DateTime.UtcNow;
        sim.ReceivedById  = userId;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Filtered list (for main log view) ────────────────────────────────

    public async Task<List<PoiSimulation>> GetFilteredAsync(
        int? brandId, DateTime? from, DateTime? to, PoiStatus? status, string? clientId = null)
    {
        var q = _db.PoiSimulations
            .Include(p => p.Brand)
            .Include(p => p.LoggedBy)
            .Include(p => p.ReceivedBy)
            .AsSplitQuery()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var term = clientId.Trim();
            q = q.Where(p => p.ClientId.Contains(term));
        }

        if (brandId.HasValue)
            q = q.Where(p => p.BrandId == brandId.Value);

        if (from.HasValue)
            q = q.Where(p => p.SimulatedAt >= from.Value.Date);

        if (to.HasValue)
            q = q.Where(p => p.SimulatedAt < to.Value.Date.AddDays(1));

        var results = await q.OrderByDescending(p => p.SimulatedAt).Take(100).ToListAsync();

        // Apply computed status filter in memory (Status is not mapped)
        if (status.HasValue)
            results = results.Where(p => p.Status == status.Value).ToList();

        return results;
    }

    // ── Report: grouped by brand for a date range ─────────────────────────

    public async Task<List<PoiSimulation>> GetReportAsync(
        int? brandId, DateTime from, DateTime to)
    {
        var q = _db.PoiSimulations
            .Include(p => p.Brand)
            .Include(p => p.LoggedBy)
            .Include(p => p.ReceivedBy)
            .AsSplitQuery()
            .Where(p => p.SimulatedAt >= from.Date && p.SimulatedAt < to.Date.AddDays(1));

        if (brandId.HasValue)
            q = q.Where(p => p.BrandId == brandId.Value);

        return await q.OrderBy(p => p.Brand!.Name)
                      .ThenByDescending(p => p.SimulatedAt)
                      .ToListAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    public async Task<List<Unified.Models.EmailTemplates.Brand>> GetBrandsAsync()
        => await _db.Brands.OrderBy(b => b.Name).ToListAsync();

    public async Task<PoiSimulation?> GetByIdAsync(int id)
        => await _db.PoiSimulations
            .Include(p => p.Brand)
            .Include(p => p.LoggedBy)
            .Include(p => p.ReceivedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<bool> ExistsForClientAndBrandAsync(string clientId, int brandId)
    {
        var normalizedClientId = clientId.Trim().ToUpper();
        return await _db.PoiSimulations.AnyAsync(p =>
            p.BrandId == brandId &&
            p.ClientId.ToUpper() == normalizedClientId);
    }
}
