using System.ComponentModel.DataAnnotations;
using Unified.Models.Identity;

namespace Unified.Models.Sip;

public class SipVote
{
    public int Id { get; set; }

    public int SipId { get; set; }

    public Sip? Sip { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public AppUser? User { get; set; }

    public bool IsUpvote { get; set; }

    public DateTime CastAt { get; set; } = DateTime.UtcNow;
}
