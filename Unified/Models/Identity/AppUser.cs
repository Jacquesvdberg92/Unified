using Microsoft.AspNetCore.Identity;

namespace Unified.Models.Identity;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Language { get; set; }
    public bool IsSwissArmyKnife { get; set; }
    public bool HasWeekendShift { get; set; }

    /// <summary>Normal hourly rate in USD used for pay calculations.</summary>
    public decimal HourlyRate { get; set; } = 0m;

    public ICollection<AgentTeam> Teams { get; set; } = new List<AgentTeam>();
    public ICollection<AgentBrand> Brands { get; set; } = new List<AgentBrand>();
}
