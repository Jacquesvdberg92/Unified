using System.ComponentModel.DataAnnotations;
using Unified.Models.Identity;

namespace Unified.Models.CsMessaging;

public class CsMessage
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string Body { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? GifUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }

    public CsConversation? Conversation { get; set; }
    public AppUser? AuthorUser { get; set; }
    public ICollection<CsMessageReaction> Reactions { get; set; } = new List<CsMessageReaction>();
}