namespace Unified.Models.Identity;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? TeamLeaderId { get; set; }
    public AppUser? TeamLeader { get; set; }

    public ICollection<AgentTeam> Members { get; set; } = new List<AgentTeam>();
}
