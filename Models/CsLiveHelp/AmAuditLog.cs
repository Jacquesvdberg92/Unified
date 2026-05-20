namespace Unified.Models.CsLiveHelp;

public class AmAuditLog
{
    public int      Id         { get; set; }
    public string   UserId     { get; set; } = string.Empty;
    public string   Action     { get; set; } = string.Empty;
    public int?     EntityId   { get; set; }
    public DateTime Timestamp  { get; set; } = DateTime.UtcNow;
    public string   IpAddress  { get; set; } = string.Empty;
}
