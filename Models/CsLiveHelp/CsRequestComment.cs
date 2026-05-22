using Unified.Models.Identity;

namespace Unified.Models.CsLiveHelp;

public class CsRequestComment
{
    public int    Id               { get; set; }

    public int    RequestId        { get; set; }
    public CsRequest? Request      { get; set; }

    public string AuthorId         { get; set; } = string.Empty;
    public AppUser? Author         { get; set; }

    public string Body             { get; set; } = string.Empty;
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;

    /// <summary>True for auto-posted system messages (e.g. "Password reset to Aa123456").</summary>
    public bool   IsSystemMessage  { get; set; }

    /// <summary>True when the comment should be visible to CS roles only (hidden from Account Managers).</summary>
    public bool   IsCsInternalOnly { get; set; }

    /// <summary>Relative URL path to an attached image (AM comments only). Null when no image attached.</summary>
    public string? ImagePath { get; set; }
}
