using Unified.Models.Identity;

namespace Unified.Models.Schedule;

public enum TimeOffType
{
    Vacation,
    DayOff,
    ScheduleChange
}

public enum TimeOffStatus
{
    Pending,
    Approved,
    Denied
}

public class TimeOffRequest
{
    public int    Id      { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public AppUser? Agent { get; set; }

    public TimeOffType   Type      { get; set; }
    public DateTime      StartDate { get; set; }
    public DateTime      EndDate   { get; set; }

    public TimeSpan? RequestedStartTime { get; set; }
    public TimeSpan? RequestedEndTime   { get; set; }

    public string Reason { get; set; } = string.Empty;

    public TimeOffStatus Status { get; set; } = TimeOffStatus.Pending;

    public string?   ReviewedByLeaderId { get; set; }
    public AppUser?  ReviewedByLeader   { get; set; }
    public DateTime? ReviewedAt         { get; set; }
    public string?   LeaderNote         { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
