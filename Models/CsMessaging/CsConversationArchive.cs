using System.ComponentModel.DataAnnotations;

namespace Unified.Models.CsMessaging;

public class CsConversationArchive
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string Body { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? GifUrl { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
}