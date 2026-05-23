using Microsoft.AspNetCore.Identity;

namespace Unified.Models.Identity;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Language { get; set; }
    public bool IsSwissArmyKnife { get; set; }
    public bool HasWeekendShift  { get; set; }
    public bool HasCsLiveHelp    { get; set; }
    /// <summary>True for Account Manager users who log in from outside the internal team.</summary>
    public bool IsExternal       { get; set; }

    /// <summary>AnyDesk remote-access ID for this user — used by the "Request Login" feature.</summary>
    public string? AnydeskId { get; set; }

    /// <summary>Normal hourly rate in USD used for pay calculations.</summary>
    public decimal HourlyRate { get; set; } = 0m;

    public ICollection<AgentTeam> Teams { get; set; } = new List<AgentTeam>();
    public ICollection<AgentBrand> Brands { get; set; } = new List<AgentBrand>();
}
