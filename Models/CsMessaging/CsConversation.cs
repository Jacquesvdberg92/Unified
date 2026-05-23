using System.ComponentModel.DataAnnotations;
using Unified.Models.Identity;

namespace Unified.Models.CsMessaging;

public class CsConversation
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string? Name { get; set; }

    public bool IsGroup { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }

    public AppUser? CreatedByUser { get; set; }
    public ICollection<CsConversationMember> Members { get; set; } = new List<CsConversationMember>();
    public ICollection<CsMessage> Messages { get; set; } = new List<CsMessage>();
}