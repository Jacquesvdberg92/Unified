using Unified.Models.EmailTemplates;
using Unified.Models.Identity;

namespace Unified.Models.CsLiveHelp;

public class CsRequest
{
    public int    Id                   { get; set; }

    public string AccountManagerId     { get; set; } = string.Empty;
    public AppUser? AccountManager     { get; set; }

    public int    BrandId              { get; set; }
    public Brand? Brand                { get; set; }

    public int    RequestTypeId        { get; set; }
    public CsRequestType? RequestType  { get; set; }

    /// <summary>Required when RequestType.IsOther is true. English-only, max 500 chars.</summary>
    public string? CustomDescription   { get; set; }

    public CsRequestStatus Status      { get; set; } = CsRequestStatus.Open;

    public DateTime CreatedAt          { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt          { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt        { get; set; }

    public ICollection<CsRequestComment> Comments { get; set; } = new List<CsRequestComment>();
}
