namespace Unified.Models.Logging;

public class ActivityLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}