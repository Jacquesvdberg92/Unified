using Unified.Models.EmailTemplates;
using Unified.Models.Identity;

namespace Unified.Models.CsLiveHelp;

/// <summary>Mirror of CsRequest for completed/old records moved by the archive background service.</summary>
public class CsRequestArchive
{
    public int    Id                  { get; set; }
    public int    OriginalRequestId   { get; set; }

    public string? AccountManagerId   { get; set; }
    public AppUser? AccountManager    { get; set; }

    public bool IsInternal             { get; set; }

    public int    BrandId             { get; set; }
    public Brand? Brand               { get; set; }

    public int    RequestTypeId       { get; set; }
    public CsRequestType? RequestType { get; set; }

    public string? CustomDescription  { get; set; }
    public string? AssignedToId        { get; set; }
    public AppUser? AssignedTo         { get; set; }

    public CsRequestStatus Status     { get; set; }

    public DateTime CreatedAt         { get; set; }
    public DateTime UpdatedAt         { get; set; }
    public DateTime ArchivedAt        { get; set; } = DateTime.UtcNow;
}
