namespace Unified.Models.Vault;

/// <summary>
/// Lightweight projection used for list/card views.
/// Does NOT include the encrypted password — that is fetched on-demand via the reveal endpoint.
/// </summary>
public class VaultEntrySummary
{
    public int     Id                  { get; set; }
    public string  Label               { get; set; } = string.Empty;
    public string  Username            { get; set; } = string.Empty;
    public string? Url                 { get; set; }
    public string? Notes               { get; set; }
    public int     CategoryId          { get; set; }
    public string? CategoryName        { get; set; }
    public string? CategoryIconCssClass { get; set; }
    public string? ProvisionedByUserId { get; set; }
}
