namespace Unified.Models.Attendance;

public class PublicHoliday
{
    public int Id { get; set; }

    /// <summary>Calendar date of the public holiday.</summary>
    public DateTime Date { get; set; }

    /// <summary>Human-readable holiday name (e.g. "New Year's Day").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description or country context.</summary>
    public string? Notes { get; set; }
}
