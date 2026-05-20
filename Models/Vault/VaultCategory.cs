using Unified.Models.Identity;

namespace Unified.Models.Vault;

public class VaultCategory
{
    public int     Id               { get; set; }
    public string  Name             { get; set; } = string.Empty;
    public string  IconCssClass     { get; set; } = "bx bx-lock";
    public bool    IsCustom         { get; set; }
    public string? CreatedByUserId  { get; set; }
    public AppUser? CreatedByUser   { get; set; }

    public ICollection<VaultEntry> Entries { get; set; } = new List<VaultEntry>();
}
