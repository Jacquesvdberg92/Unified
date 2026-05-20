namespace Unified.Models.Identity;

public class AgentTeam
{
    public string AgentId { get; set; } = string.Empty;
    public AppUser Agent { get; set; } = null!;

    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
}
