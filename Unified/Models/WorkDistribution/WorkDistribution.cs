using Unified.Models.Identity;

namespace Unified.Models.WorkDistribution;

public class WorkDistribution
{
    public int    Id          { get; set; }
    public DateTime Date      { get; set; }

    /// <summary>Raw text body of the distribution message (supports @mentions, line breaks).</summary>
    public string Body        { get; set; } = string.Empty;

    public string CreatedById { get; set; } = string.Empty;
    public AppUser? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
