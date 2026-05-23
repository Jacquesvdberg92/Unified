namespace Unified.Models.CsMessaging;

public class CsMessagingIndexViewModel
{
    public List<CsConversationListItemViewModel> Conversations { get; set; } = new();
    public CsConversationDetailViewModel? ActiveConversation { get; set; }
    public List<CsUserOptionViewModel> EligibleUsers { get; set; } = new();
    public string CurrentUserId { get; set; } = string.Empty;
}

public class CsConversationListItemViewModel
{
    public int ConversationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public int UnreadCount { get; set; }
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

public class CsConversationDetailViewModel
{
    public int ConversationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public bool CanManageMembers { get; set; }
    public List<CsConversationMemberViewModel> Members { get; set; } = new();
    public List<CsMessageViewModel> Messages { get; set; } = new();
}

public class CsConversationMemberViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class CsMessageViewModel
{
    public int MessageId { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? GifUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsMine { get; set; }
    public bool IsReadByAllOthers { get; set; }
    public List<CsMessageReactionViewModel> Reactions { get; set; } = new();
}

public class CsMessageReactionViewModel
{
    public string Emoji { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool ReactedByCurrentUser { get; set; }
}

public class CreateGroupInputModel
{
    public string Name { get; set; } = string.Empty;
    public List<string> MemberUserIds { get; set; } = new();
}

public class CsUserOptionViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class CsRecentConversationViewModel
{
    public int ConversationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public int UnreadCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

public class AddMessageInputModel
{
    public string Body { get; set; } = string.Empty;
    public string? GifUrl { get; set; }
}

public class ToggleReactionInputModel
{
    public string Emoji { get; set; } = string.Empty;
}