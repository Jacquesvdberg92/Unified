using System.ComponentModel.DataAnnotations;
using Unified.Models.Identity;

namespace Unified.Models.Sip;

public class Sip
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    public SipCategory Category { get; set; } = SipCategory.Improvement;

    public SipStatus Status { get; set; } = SipStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(2000)]
    public string? OwnerNote { get; set; }

    [StringLength(512)]
    public string? ScreenshotPath { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;

    public AppUser? Author { get; set; }

    public ICollection<SipVote> Votes { get; set; } = new List<SipVote>();
}
