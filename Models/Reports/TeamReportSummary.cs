namespace Unified.Models.Reports;

/// <summary>
/// Lightweight projection for the reports dashboard list.
/// Does NOT include AgentStats or FTDLanguageStats collections.
/// </summary>
public class TeamReportSummary
{
    public int      Id                 { get; set; }
    public string?  TeamName           { get; set; }
    public int      TotalChats         { get; set; }
    public int      TotalTickets       { get; set; }
    public int      TotalCalls         { get; set; }
    public int      TotalFTD           { get; set; }
    public DateTime SubmittedAt        { get; set; }
    public string?  LeaderDisplayName  { get; set; }
}
