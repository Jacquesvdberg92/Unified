using Unified.Models.Identity;

namespace Unified.Models.Vault;

public class VaultAccessLog
{
    public int     Id          { get; set; }
    public string  UserId      { get; set; } = string.Empty;
    public AppUser? User       { get; set; }
    public int     EntryId     { get; set; }
    public VaultEntry? Entry   { get; set; }
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    public string  Action      { get; set; } = "View"; // "View" | "Copy"
}
