using System.ComponentModel.DataAnnotations;
using Unified.Models.Identity;

namespace Unified.Models.CsMessaging;

public class CsMessageReaction
{
    public int Id { get; set; }

    public int MessageId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Emoji { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CsMessage? Message { get; set; }
    public AppUser? User { get; set; }
}