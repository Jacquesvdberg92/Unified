using Unified.Models.Identity;

namespace Unified.Models.WorkDistribution;

/// <summary>
/// Audit record for every change (assignment or swap) on a CS Live Allocation slot.
/// </summary>
public class CsLiveAllocationSwapLog
{
    public int                   Id              { get; set; }
    public int                   SlotId          { get; set; }
    public CsLiveAllocationSlot? Slot            { get; set; }

    /// <summary>Which agent position was changed: 1 or 2.</summary>
    public int              AgentPosition   { get; set; }

    public string?          PreviousAgentId { get; set; }
    public AppUser?         PreviousAgent   { get; set; }

    public string?          NewAgentId      { get; set; }
    public AppUser?         NewAgent        { get; set; }

    public string           ChangedById     { get; set; } = string.Empty;
    public AppUser?         ChangedBy       { get; set; }

    public string           Reason          { get; set; } = string.Empty;
    public DateTime         ChangedAt       { get; set; } = DateTime.UtcNow;
}
