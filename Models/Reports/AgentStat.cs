using Unified.Models.Identity;

namespace Unified.Models.Reports;

public class AgentStat
{
    public int    Id           { get; set; }

    public int    ReportId     { get; set; }
    public TeamReport? Report  { get; set; }

    public string AgentId      { get; set; } = string.Empty;
    public AppUser? Agent      { get; set; }

    public int  Chats          { get; set; }
    public int  Tickets        { get; set; }
    public int  Calls          { get; set; }
    public int  FTD            { get; set; }

    public string? Language    { get; set; }

    public bool IsTopChatPicker    { get; set; }
    public bool IsTopTicketSolver  { get; set; }
    public bool IsTopCallMaker     { get; set; }
}
