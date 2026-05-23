using System.ComponentModel.DataAnnotations;
using Unified.Models.Sip;

namespace Unified.Controllers;

public sealed class SipIndexViewModel
{
    public string Sort { get; set; } = "newest";
    public SipCategory? Category { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<SipListRowViewModel> Rows { get; set; } = [];
}

public sealed class SipListRowViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public SipCategory Category { get; set; }
    public SipStatus Status { get; set; }
    public int NetScore { get; set; }
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? OwnerNote { get; set; }
    public string? ScreenshotPath { get; set; }
}

public sealed class SipCreateInputModel
{
    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    public SipCategory Category { get; set; } = SipCategory.Improvement;
}

public sealed class SipDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SipCategory Category { get; set; }
    public SipStatus Status { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? OwnerNote { get; set; }
    public string? ScreenshotPath { get; set; }
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public int NetScore { get; set; }
    public bool? CurrentUserVoteIsUpvote { get; set; }
    public bool CanEdit { get; set; }
}

public sealed class SipAdminViewModel
{
    public SipStatus? StatusFilter { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<SipListRowViewModel> Rows { get; set; } = [];
}

public sealed class SipVoteRequest
{
    public bool IsUpvote { get; set; }
}
