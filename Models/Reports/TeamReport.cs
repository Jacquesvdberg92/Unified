using Unified.Models.Identity;

namespace Unified.Models.Reports;

public enum PeriodType { Weekly, Monthly }

public class TeamReport
{
    public int    Id                     { get; set; }

    public int    TeamId                 { get; set; }
    public Team?  Team                   { get; set; }

    public string ReportedByLeaderId     { get; set; } = string.Empty;
    public AppUser? ReportedByLeader     { get; set; }

    public PeriodType PeriodType         { get; set; }
    public DateTime   PeriodStart        { get; set; }
    public DateTime   PeriodEnd          { get; set; }

    public int  TotalChats               { get; set; }
    public int  TotalTickets             { get; set; }
    public int  TotalCalls               { get; set; }
    public int  TotalFTD                 { get; set; }

    public DateTime SubmittedAt          { get; set; } = DateTime.UtcNow;

    public ICollection<AgentStat>       AgentStats       { get; set; } = new List<AgentStat>();
    public ICollection<FTDLanguageStat> FTDLanguageStats { get; set; } = new List<FTDLanguageStat>();
}
