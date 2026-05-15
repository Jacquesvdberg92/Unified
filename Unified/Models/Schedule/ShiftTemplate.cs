namespace Unified.Models.Schedule;

public class ShiftTemplate
{
    public int    Id             { get; set; }
    public string Name          { get; set; } = string.Empty;
    public TimeSpan StartTime   { get; set; }
    public TimeSpan EndTime     { get; set; }
    public bool   IsWeekendShift { get; set; }
}
