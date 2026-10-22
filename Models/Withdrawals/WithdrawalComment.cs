using Unified.Models.Identity;

namespace Unified.Models.Withdrawals;

public class WithdrawalComment
{
    public int    Id               { get; set; }

    public int    WithdrawalId     { get; set; }
    public WithdrawalRequest? Withdrawal { get; set; }

    public string AuthorId         { get; set; } = string.Empty;
    public AppUser? Author         { get; set; }

    public string Body             { get; set; } = string.Empty;
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;

    /// <summary>True for auto-posted activity lines (picked up, status changed, …).</summary>
    public bool   IsSystemMessage  { get; set; }
}
