using Unified.Models.Identity;

namespace Unified.Models.Vault;

public class VaultEntry
{
    public int     Id                    { get; set; }

    public string  OwnerId               { get; set; } = string.Empty;
    public AppUser? Owner                { get; set; }

    public int     CategoryId            { get; set; }
    public VaultCategory? Category       { get; set; }

    public string  Label                 { get; set; } = string.Empty;
    public string  Username              { get; set; } = string.Empty;
    public string  EncryptedPassword     { get; set; } = string.Empty;
    public string? Url                   { get; set; }
    public string? Notes                 { get; set; }

    public DateTime CreatedAt            { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt            { get; set; } = DateTime.UtcNow;

    public string? ProvisionedByUserId   { get; set; }
    public AppUser? ProvisionedByUser    { get; set; }
}
