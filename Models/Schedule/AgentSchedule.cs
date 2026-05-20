using Unified.Models.Identity;

namespace Unified.Models.Schedule;

public enum ScheduleEntryType
{
    Regular,
    Custom,
    DayOff,
    Vacation
}

public class AgentSchedule
{
    public int    Id              { get; set; }
    public string AgentId         { get; set; } = string.Empty;
    public AppUser? Agent         { get; set; }

    public DateTime Date          { get; set; }

    public int? ShiftTemplateId   { get; set; }
    public ShiftTemplate? ShiftTemplate { get; set; }

    public TimeSpan? CustomStartTime { get; set; }
    public TimeSpan? CustomEndTime   { get; set; }

    public ScheduleEntryType Type { get; set; } = ScheduleEntryType.Regular;
    public string? Note           { get; set; }
}
