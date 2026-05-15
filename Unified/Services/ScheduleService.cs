using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.Schedule;

namespace Unified.Services;

public class ScheduleService
{
    private readonly AppDbContext _db;

    public ScheduleService(AppDbContext db)
    {
        _db = db;
    }

    // ── Weekly schedule ───────────────────────────────────────────────────

    public async Task<List<AgentSchedule>> GetWeeklyScheduleAsync(int teamId, DateTime weekStart)
    {
        var weekEnd = weekStart.AddDays(7);

        // Team members + SAK agents
        var agentIds = await GetTeamAgentIdsAsync(teamId);

        return await _db.AgentSchedules
            .Include(s => s.Agent)
            .Include(s => s.ShiftTemplate)
            .Where(s => agentIds.Contains(s.AgentId) && s.Date >= weekStart && s.Date < weekEnd)
            .OrderBy(s => s.AgentId).ThenBy(s => s.Date)
            .ToListAsync();
    }

    public async Task<List<AgentSchedule>> GetAgentScheduleAsync(string agentId, DateTime from, DateTime to)
        => await _db.AgentSchedules
            .Include(s => s.ShiftTemplate)
            .Where(s => s.AgentId == agentId && s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date)
            .ToListAsync();

    // ── Upsert a single day ────────────────────────────────────────────────

    public async Task<AgentSchedule> SetAgentDayAsync(AgentSchedule entry)
    {
        var existing = await _db.AgentSchedules
            .FirstOrDefaultAsync(s => s.AgentId == entry.AgentId && s.Date == entry.Date.Date);

        if (existing is null)
        {
            entry.Date = entry.Date.Date;
            _db.AgentSchedules.Add(entry);
        }
        else
        {
            existing.ShiftTemplateId  = entry.ShiftTemplateId;
            existing.CustomStartTime  = entry.CustomStartTime;
            existing.CustomEndTime    = entry.CustomEndTime;
            existing.Type             = entry.Type;
            existing.Note             = entry.Note;
        }

        await _db.SaveChangesAsync();
        return existing ?? entry;
    }

    // ── Shift templates ────────────────────────────────────────────────────

    public async Task<List<ShiftTemplate>> GetShiftTemplatesAsync()
        => await _db.ShiftTemplates.OrderBy(s => s.Name).ToListAsync();

    // ── Weekend wheel ──────────────────────────────────────────────────────

    public async Task<List<AppUser>> GetAgentsEligibleForWeekendAsync()
        => await _db.Users
            .Where(u => !u.HasWeekendShift)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

    public async Task<List<AppUser>> SpinWheelCandidatesAsync(DateTime weekStart)
    {
        var alreadyOffered = await _db.WeekendShiftOffers
            .Where(o => o.WeekStartDate == weekStart.Date)
            .Select(o => o.OfferedToAgentId)
            .ToListAsync();

        var eligible = await GetAgentsEligibleForWeekendAsync();
        return eligible
            .Where(a => !alreadyOffered.Contains(a.Id))
            .OrderBy(_ => Guid.NewGuid())   // random shuffle
            .ToList();
    }

    public async Task<WeekendShiftOffer> RecordOfferAsync(string agentId, DateTime weekStart, string leaderId)
    {
        var offer = new WeekendShiftOffer
        {
            WeekStartDate     = weekStart.Date,
            OfferedToAgentId  = agentId,
            CreatedByLeaderId = leaderId
        };
        _db.WeekendShiftOffers.Add(offer);
        await _db.SaveChangesAsync();
        return offer;
    }

    public async Task MarkOfferAcceptedAsync(int offerId)
    {
        var offer = await _db.WeekendShiftOffers.FindAsync(offerId);
        if (offer is null) return;
        offer.AcceptedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<List<WeekendShiftOffer>> GetOffersForWeekAsync(DateTime weekStart)
        => await _db.WeekendShiftOffers
            .Include(o => o.OfferedToAgent)
            .Where(o => o.WeekStartDate == weekStart.Date)
            .OrderBy(o => o.OfferedToAgent!.DisplayName)
            .ToListAsync();

    // ── Time-off requests ─────────────────────────────────────────────────

    public async Task<TimeOffRequest> SubmitTimeOffRequestAsync(TimeOffRequest request)
    {
        // Block overlapping pending/approved requests
        var hasOverlap = await _db.TimeOffRequests.AnyAsync(r =>
            r.AgentId == request.AgentId &&
            r.Status  != TimeOffStatus.Denied &&
            r.StartDate <= request.EndDate &&
            r.EndDate   >= request.StartDate);

        if (hasOverlap)
            throw new InvalidOperationException("You already have a pending or approved request for those dates.");

        request.CreatedAt = DateTime.UtcNow;
        request.Status    = TimeOffStatus.Pending;
        _db.TimeOffRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<List<TimeOffRequest>> GetPendingRequestsAsync(int teamId)
    {
        var agentIds = await GetTeamAgentIdsAsync(teamId);
        return await _db.TimeOffRequests
            .Include(r => r.Agent)
            .Where(r => agentIds.Contains(r.AgentId) && r.Status == TimeOffStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<TimeOffRequest>> GetMyRequestsAsync(string agentId)
        => await _db.TimeOffRequests
            .Include(r => r.ReviewedByLeader)
            .Where(r => r.AgentId == agentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public async Task ApproveRequestAsync(int requestId, string leaderId, string? leaderNote)
    {
        var request = await _db.TimeOffRequests.FindAsync(requestId)
            ?? throw new KeyNotFoundException("Request not found.");

        request.Status             = TimeOffStatus.Approved;
        request.ReviewedByLeaderId = leaderId;
        request.ReviewedAt         = DateTime.UtcNow;
        request.LeaderNote         = leaderNote;

        // Auto-create schedule entries for vacation / day-off
        if (request.Type == TimeOffType.Vacation || request.Type == TimeOffType.DayOff)
        {
            var entryType = request.Type == TimeOffType.Vacation
                ? ScheduleEntryType.Vacation
                : ScheduleEntryType.DayOff;

            for (var d = request.StartDate.Date; d <= request.EndDate.Date; d = d.AddDays(1))
            {
                var exists = await _db.AgentSchedules
                    .AnyAsync(s => s.AgentId == request.AgentId && s.Date == d);
                if (!exists)
                    _db.AgentSchedules.Add(new AgentSchedule
                    {
                        AgentId = request.AgentId,
                        Date    = d,
                        Type    = entryType
                    });
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task DenyRequestAsync(int requestId, string leaderId, string? leaderNote)
    {
        var request = await _db.TimeOffRequests.FindAsync(requestId)
            ?? throw new KeyNotFoundException("Request not found.");

        request.Status             = TimeOffStatus.Denied;
        request.ReviewedByLeaderId = leaderId;
        request.ReviewedAt         = DateTime.UtcNow;
        request.LeaderNote         = leaderNote;
        await _db.SaveChangesAsync();
    }

    // ── Pending count helper (used by WeekView badge) ─────────────────────

    public async Task<int> GetPendingCountForTeamAsync(int teamId)
    {
        var agentIds = await GetTeamAgentIdsAsync(teamId);
        return await _db.TimeOffRequests
            .CountAsync(r => agentIds.Contains(r.AgentId) && r.Status == TimeOffStatus.Pending);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<List<string>> GetTeamAgentIdsAsync(int teamId)
    {
        var teamMembers = await _db.AgentTeams
            .Where(at => at.TeamId == teamId)
            .Select(at => at.AgentId)
            .ToListAsync();

        var sakIds = await _db.Users
            .Where(u => u.IsSwissArmyKnife)
            .Select(u => u.Id)
            .ToListAsync();

        return teamMembers.Union(sakIds).Distinct().ToList();
    }
}
