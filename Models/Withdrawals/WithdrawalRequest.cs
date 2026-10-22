using Unified.Models.EmailTemplates;
using Unified.Models.Identity;

namespace Unified.Models.Withdrawals;

public class WithdrawalRequest
{
    public int    Id                  { get; set; }

    /// <summary>The client ID (CL ID) the withdrawal belongs to.</summary>
    public string ClientId            { get; set; } = string.Empty;

    public int    BrandId             { get; set; }
    public Brand? Brand               { get; set; }

    /// <summary>Optional withdrawal amount.</summary>
    public decimal? Amount            { get; set; }

    /// <summary>Optional ISO currency code, e.g. EUR.</summary>
    public string? Currency           { get; set; }

    /// <summary>Documents still missing from the client.</summary>
    public WithdrawalDocuments MissingDocuments { get; set; }

    /// <summary>Free text links or ticket numbers, on or off the platform. One per line.</summary>
    public string? RelatedTickets     { get; set; }

    public WithdrawalStatus Status    { get; set; } = WithdrawalStatus.Open;

    /// <summary>The user who picked this withdrawal up. Null while unassigned.</summary>
    public string? AssignedToId       { get; set; }
    public AppUser? AssignedTo        { get; set; }

    public string  CreatedById        { get; set; } = string.Empty;
    public AppUser? CreatedBy         { get; set; }

    public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt         { get; set; } = DateTime.UtcNow;

    public ICollection<WithdrawalComment> Comments { get; set; } = new List<WithdrawalComment>();
}
