namespace Unified.Controllers;

public sealed class PendingAccountManagerViewModel
{
    public int AccountRequestId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string? UserId { get; set; }
    public bool IsLockedOut { get; set; }
    public bool EmailConfirmed { get; set; }
}
