namespace Unified.Models.Identity;

public enum AccountRequestStatus
{
    Pending,
    Approved,
    Rejected
}

public class AccountRequest
{
    public int    Id          { get; set; }
    public string FullName    { get; set; } = string.Empty;
    public string Email       { get; set; } = string.Empty;
    public string Role        { get; set; } = string.Empty;
    public string? Message    { get; set; }
    public AccountRequestStatus Status { get; set; } = AccountRequestStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}
