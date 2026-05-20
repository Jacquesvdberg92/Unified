using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.WorkDistribution;

namespace Unified.Services;

public class WorkDistributionService
{
    private readonly AppDbContext _db;
    public WorkDistributionService(AppDbContext db) => _db = db;

    public async Task<WorkDistribution?> GetForDateAsync(DateTime date)
        => await _db.WorkDistributions
            .Include(w => w.CreatedBy)
            .FirstOrDefaultAsync(w => w.Date == date.Date);

    public async Task<List<WorkDistribution>> GetRecentAsync(int count = 14)
        => await _db.WorkDistributions
            .Include(w => w.CreatedBy)
            .OrderByDescending(w => w.Date)
            .Take(count)
            .ToListAsync();

    public async Task SaveAsync(DateTime date, string body, string createdById)
    {
        var existing = await _db.WorkDistributions
            .FirstOrDefaultAsync(w => w.Date == date.Date);

        if (existing != null)
        {
            existing.Body      = body;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.WorkDistributions.Add(new WorkDistribution
            {
                Date        = date.Date,
                Body        = body,
                CreatedById = createdById,
                CreatedAt   = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var item = await _db.WorkDistributions.FindAsync(id);
        if (item != null)
        {
            _db.WorkDistributions.Remove(item);
            await _db.SaveChangesAsync();
        }
    }
}
