using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.WorkDistribution;

namespace Unified.Services;

public class CsLiveHelpService
{
    private readonly AppDbContext _db;

    public CsLiveHelpService(AppDbContext db) => _db = db;

    public static IEnumerable<int> SlotHours => Enumerable.Range(8, 13); // 8..20

    // ── Queries ────────────────────────────────────────────────────────────

    public async Task<List<CsLiveHelpSlot>> GetSlotsForDateAsync(DateTime date)
        => await _db.CsLiveHelpSlots
            .Include(s => s.Agent1)
            .Include(s => s.Agent2)
            .Include(s => s.CreatedBy)
            .Where(s => s.Date == date.Date)
            .OrderBy(s => s.SlotHour)
            .ToListAsync();

    public async Task<List<AppUser>> GetEligibleAgentsAsync()
        => await _db.Users
            .Where(u => u.HasCsLiveHelp)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

    public async Task<List<CsLiveHelpSwapLog>> GetSwapLogAsync(DateTime? date = null, int pageSize = 100)
    {
        var q = _db.CsLiveHelpSwapLogs
            .Include(l => l.Slot)
            .Include(l => l.PreviousAgent)
            .Include(l => l.NewAgent)
            .Include(l => l.ChangedBy)
            .AsQueryable();

        if (date.HasValue)
            q = q.Where(l => l.Slot!.Date == date.Value.Date);

        return await q.OrderByDescending(l => l.ChangedAt).Take(pageSize).ToListAsync();
    }

    // ── Generate (overwrite all slots for a day) ───────────────────────────

    /// <summary>
    /// Saves a full day's schedule. <paramref name="assignments"/> maps SlotHour → (Agent1Id, Agent2Id).
    /// Existing slots for that day are replaced.
    /// </summary>
    public async Task GenerateScheduleAsync(
        DateTime date,
        Dictionary<int, (string? a1, string? a2)> assignments,
        string createdById)
    {
        var existing = await _db.CsLiveHelpSlots
            .Where(s => s.Date == date.Date)
            .ToListAsync();

        _db.CsLiveHelpSlots.RemoveRange(existing);

        foreach (var hour in SlotHours)
        {
            var (a1, a2) = assignments.TryGetValue(hour, out var val) ? val : (null, null);
            _db.CsLiveHelpSlots.Add(new CsLiveHelpSlot
            {
                Date        = date.Date,
                SlotHour    = hour,
                Agent1Id    = string.IsNullOrWhiteSpace(a1) ? null : a1,
                Agent2Id    = string.IsNullOrWhiteSpace(a2) ? null : a2,
                CreatedById = createdById,
                CreatedAt   = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    // ── Swap / reassign a single agent position ────────────────────────────

    public async Task SwapAgentAsync(
        int slotId,
        int agentPosition,   // 1 or 2
        string? newAgentId,
        string changedById,
        string reason)
    {
        var slot = await _db.CsLiveHelpSlots.FindAsync(slotId)
            ?? throw new InvalidOperationException("Slot not found.");

        var previousId = agentPosition == 1 ? slot.Agent1Id : slot.Agent2Id;

        if (agentPosition == 1) slot.Agent1Id = string.IsNullOrWhiteSpace(newAgentId) ? null : newAgentId;
        else                    slot.Agent2Id = string.IsNullOrWhiteSpace(newAgentId) ? null : newAgentId;

        slot.UpdatedAt = DateTime.UtcNow;

        _db.CsLiveHelpSwapLogs.Add(new CsLiveHelpSwapLog
        {
            SlotId          = slotId,
            AgentPosition   = agentPosition,
            PreviousAgentId = previousId,
            NewAgentId      = string.IsNullOrWhiteSpace(newAgentId) ? null : newAgentId,
            ChangedById     = changedById,
            Reason          = reason,
            ChangedAt       = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
