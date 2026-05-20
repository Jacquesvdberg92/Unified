using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Attendance;

namespace Unified.Services;

public class AttendanceReportRow
{
    public int    LogId          { get; set; }
    public string AgentId        { get; set; } = string.Empty;
    public string AgentName      { get; set; } = string.Empty;
    public DateTime WorkDate     { get; set; }
    public DateTime? CheckIn     { get; set; }
    public DateTime? CheckOut    { get; set; }
    public double?  BillableHours { get; set; }
    public DayPayType PayType    { get; set; }
    public AttendanceStatus Status { get; set; }
    public decimal PayAmount     { get; set; }
}

public class AttendanceService
{
    private readonly AppDbContext _db;

    // Weekend flat rate USD
    public const decimal WeekendDayRate = 80m;

    public AttendanceService(AppDbContext db) => _db = db;

    // ── Clock In ────────────────────────────────────────────────────────────

    /// <summary>
    /// Clock the agent in. Creates a new AttendanceLog for today if none exists,
    /// or updates an existing one that has no CheckInTime yet.
    /// </summary>
    public async Task<AttendanceLog> ClockInAsync(string agentId)
    {
        var today = DateTime.UtcNow.Date;
        var existing = await _db.AttendanceLogs
            .FirstOrDefaultAsync(l => l.AgentId == agentId && l.WorkDate == today);

        if (existing != null && existing.CheckInTime.HasValue)
            throw new InvalidOperationException("Already clocked in today.");

        var payType = await ResolveDayPayTypeAsync(today);

        if (existing == null)
        {
            existing = new AttendanceLog
            {
                AgentId     = agentId,
                WorkDate    = today,
                CheckInTime = DateTime.UtcNow,
                PayType     = payType,
                Status      = AttendanceStatus.Present
            };
            _db.AttendanceLogs.Add(existing);
        }
        else
        {
            existing.CheckInTime = DateTime.UtcNow;
            existing.PayType     = payType;
            existing.Status      = AttendanceStatus.Present;
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    // ── Clock Out ───────────────────────────────────────────────────────────

    public async Task<AttendanceLog> ClockOutAsync(string agentId)
    {
        var today = DateTime.UtcNow.Date;
        var log = await _db.AttendanceLogs
            .FirstOrDefaultAsync(l => l.AgentId == agentId && l.WorkDate == today);

        if (log == null || !log.CheckInTime.HasValue)
            throw new InvalidOperationException("No active clock-in found for today.");

        if (log.CheckOutTime.HasValue)
            throw new InvalidOperationException("Already clocked out today.");

        log.CheckOutTime = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return log;
    }

    // ── Retrospective ───────────────────────────────────────────────────────

    public async Task<AttendanceLog> SubmitRetrospectiveAsync(
        string agentId,
        DateTime workDate,
        DateTime checkIn,
        DateTime checkOut,
        string? note)
    {
        var date = workDate.Date;
        var existing = await _db.AttendanceLogs
            .FirstOrDefaultAsync(l => l.AgentId == agentId && l.WorkDate == date);

        if (existing != null && existing.Status == AttendanceStatus.Approved)
            throw new InvalidOperationException("An approved entry already exists for this date.");

        var payType = await ResolveDayPayTypeAsync(date);

        if (existing == null)
        {
            existing = new AttendanceLog
            {
                AgentId      = agentId,
                WorkDate     = date,
                CheckInTime  = checkIn,
                CheckOutTime = checkOut,
                PayType      = payType,
                Status       = AttendanceStatus.Present,
                AgentNote    = note
            };
            _db.AttendanceLogs.Add(existing);
        }
        else
        {
            existing.CheckInTime  = checkIn;
            existing.CheckOutTime = checkOut;
            existing.PayType      = payType;
            existing.Status       = AttendanceStatus.Present;
            existing.AgentNote    = note;
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    // ── Edit existing log times ──────────────────────────────────────────────

    /// <summary>
    /// Allows an agent to correct their own check-in / check-out times for a given log.
    /// </summary>
    public async Task UpdateTimesAsync(int logId, string agentId, DateTime? checkIn, DateTime? checkOut)
    {
        var log = await _db.AttendanceLogs.FindAsync(logId)
            ?? throw new KeyNotFoundException("Attendance log not found.");

        if (log.AgentId != agentId)
            throw new UnauthorizedAccessException("You can only edit your own attendance.");

        if (log.Status == AttendanceStatus.Approved)
            throw new InvalidOperationException("Approved entries cannot be edited.");

        if (checkIn.HasValue && checkOut.HasValue && checkOut <= checkIn)
            throw new InvalidOperationException("Check-out must be after check-in.");

        log.CheckInTime  = checkIn;
        log.CheckOutTime = checkOut;

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Allows a manager / team leader to fix any agent's check-in / check-out times.
    /// </summary>
    public async Task ManagerUpdateTimesAsync(int logId, DateTime? checkIn, DateTime? checkOut)
    {
        var log = await _db.AttendanceLogs.FindAsync(logId)
            ?? throw new KeyNotFoundException("Attendance log not found.");

        if (checkIn.HasValue && checkOut.HasValue && checkOut <= checkIn)
            throw new InvalidOperationException("Check-out must be after check-in.");

        log.CheckInTime  = checkIn;
        log.CheckOutTime = checkOut;

        await _db.SaveChangesAsync();
    }

    // ── Review (approve / reject) ───────────────────────────────────────────

    public async Task ReviewRetrospectiveAsync(
        int logId,
        string reviewerId,
        bool approved,
        string? reviewerNote)
    {
        var log = await _db.AttendanceLogs.FindAsync(logId)
            ?? throw new KeyNotFoundException("Attendance log not found.");

        log.Status       = approved ? AttendanceStatus.Approved : AttendanceStatus.Rejected;
        log.ReviewedById = reviewerId;
        log.ReviewerNote = reviewerNote;
        log.ReviewedAt   = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Today status ────────────────────────────────────────────────────────

    public async Task<AttendanceLog?> GetTodayLogAsync(string agentId)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.AttendanceLogs
            .FirstOrDefaultAsync(l => l.AgentId == agentId && l.WorkDate == today);
    }

    public async Task<AttendanceLog?> GetLogByIdAsync(int id)
        => await _db.AttendanceLogs.FindAsync(id);

    // ── Agent history ────────────────────────────────────────────────────────

    public async Task<List<AttendanceLog>> GetAgentHistoryAsync(
        string agentId,
        DateTime from,
        DateTime to)
    {
        return await _db.AttendanceLogs
            .Where(l => l.AgentId == agentId
                     && l.WorkDate >= from.Date
                     && l.WorkDate <= to.Date)
            .OrderByDescending(l => l.WorkDate)
            .ToListAsync();
    }

    // ── Pending retrospectives (for managers) ──────────────────────────────

    public async Task<List<AttendanceLog>> GetPendingRetrospectivesAsync()
    {
        return await _db.AttendanceLogs
            .Include(l => l.Agent)
            .Where(l => l.Status == AttendanceStatus.Retrospective)
            .OrderBy(l => l.WorkDate)
            .ToListAsync();
    }

    // ── Report generation ───────────────────────────────────────────────────

    public async Task<List<AttendanceReportRow>> GenerateReportAsync(
        DateTime from,
        DateTime to,
        string? agentId = null)
    {
        var query = _db.AttendanceLogs
            .Include(l => l.Agent)
            .Where(l => l.WorkDate >= from.Date
                     && l.WorkDate <= to.Date
                     && l.Status != AttendanceStatus.Rejected);

        if (!string.IsNullOrWhiteSpace(agentId))
            query = query.Where(l => l.AgentId == agentId);

        var logs = await query.OrderBy(l => l.AgentId).ThenBy(l => l.WorkDate).ToListAsync();

        var rows = new List<AttendanceReportRow>();

        foreach (var log in logs)
        {
            var pay = CalculatePay(log, log.Agent?.HourlyRate ?? 0m);
            rows.Add(new AttendanceReportRow
            {
                LogId         = log.Id,
                AgentId       = log.AgentId,
                AgentName     = log.Agent?.DisplayName ?? log.AgentId,
                WorkDate      = log.WorkDate,
                CheckIn       = log.CheckInTime,
                CheckOut      = log.CheckOutTime,
                BillableHours = log.BillableHours,
                PayType       = log.PayType,
                Status        = log.Status,
                PayAmount     = pay
            });
        }

        return rows;
    }

    // ── Pay calculation ──────────────────────────────────────────────────────

    public static decimal CalculatePay(AttendanceLog log, decimal hourlyRate)
    {
        return log.PayType switch
        {
            DayPayType.DayOff       => 0m,
            DayPayType.Weekend      => 0m, // shown as label in reports, not a calculated amount
            DayPayType.PublicHoliday =>
                // Double pay if worked, 0 if elected off (no CheckIn)
                log.CheckInTime.HasValue
                    ? (decimal)(log.BillableHours ?? 0) * hourlyRate * 2m
                    : 0m,
            DayPayType.Vacation     =>
                // Paid at normal rate × billable hours (use scheduled hours if no clocked time)
                (decimal)(log.BillableHours ?? 8.0) * hourlyRate,
            _                        =>
                // Regular — hourly × billable hours
                (decimal)(log.BillableHours ?? 0) * hourlyRate
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<DayPayType> ResolveDayPayTypeAsync(DateTime date)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return DayPayType.Weekend;

        var isHoliday = await _db.PublicHolidays
            .AnyAsync(h => h.Date.Date == date.Date);

        return isHoliday ? DayPayType.PublicHoliday : DayPayType.Regular;
    }

    // ── Public Holiday management ────────────────────────────────────────────

    public async Task<List<PublicHoliday>> GetHolidaysAsync(int year)
        => await _db.PublicHolidays
            .Where(h => h.Date.Year == year)
            .OrderBy(h => h.Date)
            .ToListAsync();

    public async Task AddHolidayAsync(PublicHoliday holiday)
    {
        _db.PublicHolidays.Add(holiday);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteHolidayAsync(int id)
    {
        var h = await _db.PublicHolidays.FindAsync(id);
        if (h != null)
        {
            _db.PublicHolidays.Remove(h);
            await _db.SaveChangesAsync();
        }
    }
}
