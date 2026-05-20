using Unified.Models.Identity;

namespace Unified.Models.Schedule;

public class WeekendShiftOffer
{
    public int      Id                { get; set; }
    public DateTime WeekStartDate     { get; set; }

    public string   OfferedToAgentId  { get; set; } = string.Empty;
    public AppUser? OfferedToAgent    { get; set; }

    public DateTime? AcceptedAt       { get; set; }

    public string   CreatedByLeaderId { get; set; } = string.Empty;
    public AppUser? CreatedByLeader   { get; set; }
}
