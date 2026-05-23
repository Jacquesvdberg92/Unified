using System.ComponentModel.DataAnnotations;
using Unified.Models.Identity;

namespace Unified.Models.CsMessaging;

public class CsConversationMember
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public DateTime? LastReadAt { get; set; }

    public CsConversation? Conversation { get; set; }
    public AppUser? User { get; set; }
}