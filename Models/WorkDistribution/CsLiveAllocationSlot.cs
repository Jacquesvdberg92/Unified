using Unified.Models.Identity;

namespace Unified.Models.WorkDistribution;

/// <summary>
/// Represents one hourly CS Live Allocation slot.
/// SlotHour = 8 means 08:00–09:00, SlotHour = 20 means 20:00–21:00.
/// Each slot holds up to two assigned agents.
/// </summary>
public class CsLiveAllocationSlot
{
    public int      Id          { get; set; }
    public DateTime Date        { get; set; }
    public int      SlotHour    { get; set; }   // 8–20

    public string?  Agent1Id    { get; set; }
    public AppUser? Agent1      { get; set; }

    public string?  Agent2Id    { get; set; }
    public AppUser? Agent2      { get; set; }

    public string   CreatedById { get; set; } = string.Empty;
    public AppUser? CreatedBy   { get; set; }

    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt   { get; set; } = DateTime.UtcNow;
}
