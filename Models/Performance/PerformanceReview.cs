using Unified.Models.Identity;

namespace Unified.Models.Performance;

public class PerformanceReview
{
    public int    Id                    { get; set; }

    public string AgentId               { get; set; } = string.Empty;
    public AppUser? Agent               { get; set; }

    public string ReviewedByLeaderId    { get; set; } = string.Empty;
    public AppUser? ReviewedByLeader    { get; set; }

    public DateTime ReviewDate          { get; set; }
    public string   PeriodLabel         { get; set; } = string.Empty;
    public string?  OverallNotes        { get; set; }

    public DateTime CreatedAt           { get; set; } = DateTime.UtcNow;

    public ICollection<ReviewItem> Items { get; set; } = new List<ReviewItem>();
}
