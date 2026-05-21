using Unified.Models.EmailTemplates;
using Unified.Models.Identity;

namespace Unified.Models.CsLiveHelp;

public class CsRequest
{
    public int    Id                   { get; set; }

    public string? AccountManagerId    { get; set; }
    public AppUser? AccountManager     { get; set; }

    /// <summary>True when this request was posted directly by a CS agent (not an AM).</summary>
    public bool IsInternal              { get; set; }

    public int    BrandId              { get; set; }
    public Brand? Brand                { get; set; }

    public int    RequestTypeId        { get; set; }
    public CsRequestType? RequestType  { get; set; }

    /// <summary>The client/player ID this request is about. Required on submission.</summary>
    public string? ClientId            { get; set; }

    /// <summary>Required when RequestType.IsOther is true. English-only, max 500 chars.</summary>
    public string? CustomDescription   { get; set; }

    /// <summary>The CS agent who last moved this card. Set automatically on status transition.</summary>
    public string? AssignedToId         { get; set; }
    public AppUser? AssignedTo          { get; set; }

    public CsRequestStatus Status      { get; set; } = CsRequestStatus.Open;

    public DateTime CreatedAt          { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt          { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt        { get; set; }

    public ICollection<CsRequestComment> Comments { get; set; } = new List<CsRequestComment>();
}
