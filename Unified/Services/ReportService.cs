using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Reports;
using Unified.Services;

namespace Unified.Services;

public class ReportService
{
    private readonly AppDbContext        _db;
    private readonly PerformanceService  _perf;

    public ReportService(AppDbContext db, PerformanceService perf)
    {
        _db   = db;
        _perf = perf;
    }

    // ── Submit ────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates, computes top-performer flags, builds FTD-by-language rows,
    /// then saves the full report in a single transaction.
    /// </summary>
    public async Task<TeamReport> SubmitReportAsync(TeamReport report)
    {
        if (report.PeriodEnd < report.PeriodStart)
            throw new ArgumentException("PeriodEnd must be on or after PeriodStart.");

        if (!report.AgentStats.Any())
            throw new ArgumentException("At least one agent stat row is required.");

        ComputeTopPerformerFlags(report.AgentStats);

        // Build FTD-by-language aggregation from agent stats
        var ftdByLang = report.AgentStats
            .Where(s => !string.IsNullOrWhiteSpace(s.Language))
            .GroupBy(s => s.Language!)
            .Select(g => new FTDLanguageStat
            {
                Language = g.Key,
                FTDCount = g.Sum(s => s.FTD)
            })
            .ToList();

        foreach (var f in ftdByLang)
            report.FTDLanguageStats.Add(f);

        // Sync totals from agent rows
        report.TotalChats   = report.AgentStats.Sum(s => s.Chats);
        report.TotalTickets = report.AgentStats.Sum(s => s.Tickets);
        report.TotalCalls   = report.AgentStats.Sum(s => s.Calls);
        report.TotalFTD     = report.AgentStats.Sum(s => s.FTD);

        report.SubmittedAt  = DateTime.UtcNow;

        _db.TeamReports.Add(report);
        await _db.SaveChangesAsync();
        return report;
    }

    // ── Read ──────────────────────────────────────────────────────────────

    /// <summary>
    /// High-level KPI cards — aggregates across ALL teams for the given period.
    /// </summary>
    public async Task<ReportSummaryDto> GetReportSummaryAsync(
        PeriodType periodType, DateTime periodStart)
    {
        var reports = await _db.TeamReports
            .Where(r => r.PeriodType == periodType && r.PeriodStart == periodStart)
            .Include(r => r.Team)
            .ToListAsync();

        return new ReportSummaryDto
        {
            PeriodType   = periodType,
            PeriodStart  = periodStart,
            TotalChats   = reports.Sum(r => r.TotalChats),
            TotalTickets = reports.Sum(r => r.TotalTickets),
            TotalCalls   = reports.Sum(r => r.TotalCalls),
            TotalFTD     = reports.Sum(r => r.TotalFTD),
            TeamReports  = reports
        };
    }

    /// <summary>All team reports, ordered newest first.</summary>
    public async Task<List<TeamReport>> GetAllReportsAsync(PeriodType? periodType = null)
    {
        var q = _db.TeamReports
            .Include(r => r.Team)
            .Include(r => r.ReportedByLeader)
            .AsQueryable();

        if (periodType.HasValue)
            q = q.Where(r => r.PeriodType == periodType.Value);

        return await q.OrderByDescending(r => r.PeriodStart).ToListAsync();
    }

    /// <summary>Full report with agent breakdown and FTD-by-language.</summary>
    public async Task<TeamReport?> GetTeamBreakdownAsync(int reportId)
        => await _db.TeamReports
            .Include(r => r.Team)
            .Include(r => r.ReportedByLeader)
            .Include(r => r.AgentStats).ThenInclude(s => s.Agent)
            .Include(r => r.FTDLanguageStats)
            .FirstOrDefaultAsync(r => r.Id == reportId);

    /// <summary>FTD grouped by language for a single report.</summary>
    public async Task<List<FTDLanguageStat>> GetFTDByLanguageAsync(int reportId)
        => await _db.FTDLanguageStats
            .Where(f => f.ReportId == reportId)
            .OrderByDescending(f => f.FTDCount)
            .ToListAsync();

    /// <summary>
    /// Combines agent-stat top-performer flags with average review scores
    /// from PerformanceService for the same period.
    /// </summary>
    public async Task<List<PerformanceHighlightDto>> GetPerformanceHighlightsAsync(int reportId)
    {
        var report = await GetTeamBreakdownAsync(reportId);
        if (report is null) return new();

        var highlights = new List<PerformanceHighlightDto>();

        foreach (var stat in report.AgentStats)
        {
            var avg = await _perf.GetAverageRatingAsync(
                stat.AgentId, from: report.PeriodStart, to: report.PeriodEnd);

            highlights.Add(new PerformanceHighlightDto
            {
                Agent            = stat.Agent,
                Chats            = stat.Chats,
                Tickets          = stat.Tickets,
                Calls            = stat.Calls,
                FTD              = stat.FTD,
                IsTopChatPicker  = stat.IsTopChatPicker,
                IsTopTicketSolver= stat.IsTopTicketSolver,
                IsTopCallMaker   = stat.IsTopCallMaker,
                AvgReviewScore   = avg
            });
        }

        return highlights.OrderByDescending(h => h.FTD).ToList();
    }

    public async Task DeleteReportAsync(int id)
    {
        var r = await _db.TeamReports.FindAsync(id);
        if (r is null) return;
        _db.TeamReports.Remove(r);
        await _db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void ComputeTopPerformerFlags(ICollection<AgentStat> stats)
    {
        if (!stats.Any()) return;

        int maxChats   = stats.Max(s => s.Chats);
        int maxTickets = stats.Max(s => s.Tickets);
        int maxCalls   = stats.Max(s => s.Calls);

        foreach (var s in stats)
        {
            s.IsTopChatPicker   = s.Chats   == maxChats   && maxChats   > 0;
            s.IsTopTicketSolver = s.Tickets == maxTickets && maxTickets > 0;
            s.IsTopCallMaker    = s.Calls   == maxCalls   && maxCalls   > 0;
        }
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────

public class ReportSummaryDto
{
    public PeriodType       PeriodType   { get; set; }
    public DateTime         PeriodStart  { get; set; }
    public int              TotalChats   { get; set; }
    public int              TotalTickets { get; set; }
    public int              TotalCalls   { get; set; }
    public int              TotalFTD     { get; set; }
    public List<TeamReport> TeamReports  { get; set; } = new();
}

public class PerformanceHighlightDto
{
    public Unified.Models.Identity.AppUser? Agent { get; set; }
    public int    Chats             { get; set; }
    public int    Tickets           { get; set; }
    public int    Calls             { get; set; }
    public int    FTD               { get; set; }
    public bool   IsTopChatPicker   { get; set; }
    public bool   IsTopTicketSolver { get; set; }
    public bool   IsTopCallMaker    { get; set; }
    public double? AvgReviewScore   { get; set; }
}
