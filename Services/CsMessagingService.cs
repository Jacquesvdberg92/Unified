using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.CsMessaging;
using Unified.Models.Identity;

namespace Unified.Services;

public class CsMessagingService
{
    private static readonly HashSet<string> ChatRoles =
    [
        Roles.CSAgent,
        Roles.TeamLeader,
        Roles.BrandManager,
        Roles.SwissArmyKnife
    ];

    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;

    public CsMessagingService(AppDbContext db, UserManager<AppUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<List<CsUserOptionViewModel>> GetEligibleUsersAsync(string currentUserId)
    {
        var byId = new Dictionary<string, AppUser>(StringComparer.Ordinal);
        foreach (var role in ChatRoles)
        {
            var users = await _users.GetUsersInRoleAsync(role);
            foreach (var user in users)
            {
                if (user.Id == currentUserId) continue;
                byId[user.Id] = user;
            }
        }

        return byId.Values
            .OrderBy(u => u.DisplayName)
            .Select(u => new CsUserOptionViewModel
            {
                UserId = u.Id,
                DisplayName = string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName ?? u.Id : u.DisplayName
            })
            .ToList();
    }

    public async Task<List<CsConversationListItemViewModel>> GetUserConversationsAsync(string userId)
    {
        var conversations = await _db.CsConversations
            .Where(c => !c.IsArchived)
            .Where(c => c.Members.Any(m => m.UserId == userId && m.IsActive))
            .Include(c => c.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.User)
            .Include(c => c.Messages.OrderByDescending(mm => mm.CreatedAt).Take(1))
            .ToListAsync();

        var list = new List<CsConversationListItemViewModel>(conversations.Count);
        foreach (var conv in conversations)
        {
            var lastMessage = conv.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
            var me = conv.Members.FirstOrDefault(m => m.UserId == userId);
            var unread = 0;
            if (me != null)
            {
                unread = await _db.CsMessages.CountAsync(m =>
                    m.ConversationId == conv.Id &&
                    !m.IsDeleted &&
                    m.AuthorUserId != userId &&
                    m.CreatedAt > (me.LastReadAt ?? DateTime.MinValue));
            }

            list.Add(new CsConversationListItemViewModel
            {
                ConversationId = conv.Id,
                DisplayName = BuildConversationDisplayName(conv, userId),
                IsGroup = conv.IsGroup,
                UnreadCount = unread,
                LastMessagePreview = BuildLastMessagePreview(lastMessage),
                LastMessageAt = lastMessage?.CreatedAt
            });
        }

        return list
            .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
            .ToList();
    }

    public async Task<CsConversationDetailViewModel?> GetConversationDetailAsync(int conversationId, string userId, int take = 50)
    {
        var isMember = await _db.CsConversationMembers.AnyAsync(m =>
            m.ConversationId == conversationId &&
            m.UserId == userId &&
            m.IsActive);

        if (!isMember) return null;

        var conversation = await _db.CsConversations
            .Include(c => c.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(c => c.Id == conversationId && !c.IsArchived);

        if (conversation is null) return null;

        var messages = await _db.CsMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Include(m => m.AuthorUser)
            .Include(m => m.Reactions)
            .ToListAsync();

        messages.Reverse();

        var activeMembers = conversation.Members
            .Where(m => m.IsActive)
            .ToList();

        return new CsConversationDetailViewModel
        {
            ConversationId = conversation.Id,
            DisplayName = BuildConversationDisplayName(conversation, userId),
            IsGroup = conversation.IsGroup,
            CanManageMembers = conversation.IsGroup && conversation.CreatedByUserId == userId,
            Members = activeMembers
                .OrderBy(m => m.User!.DisplayName)
                .Select(m => new CsConversationMemberViewModel
                {
                    UserId = m.UserId,
                    DisplayName = m.User?.DisplayName ?? m.User?.UserName ?? m.UserId
                })
                .ToList(),
            Messages = messages.Select(m => ToMessageViewModel(m, userId, activeMembers)).ToList()
        };
    }

    public async Task<int?> StartDirectAsync(string currentUserId, string otherUserId)
    {
        if (currentUserId == otherUserId) return null;

        var existing = await _db.CsConversations
            .Where(c => !c.IsGroup && !c.IsArchived)
            .Where(c => c.Members.Count(m => m.IsActive) == 2)
            .Where(c => c.Members.Any(m => m.UserId == currentUserId && m.IsActive))
            .Where(c => c.Members.Any(m => m.UserId == otherUserId && m.IsActive))
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (existing != 0)
            return existing;

        var now = DateTime.UtcNow;
        var conversation = new CsConversation
        {
            IsGroup = false,
            CreatedByUserId = currentUserId,
            CreatedAt = now,
            UpdatedAt = now,
            Members =
            [
                new CsConversationMember { UserId = currentUserId, JoinedAt = now, IsActive = true, LastReadAt = now },
                new CsConversationMember { UserId = otherUserId, JoinedAt = now, IsActive = true, LastReadAt = now }
            ]
        };

        _db.CsConversations.Add(conversation);
        await _db.SaveChangesAsync();
        return conversation.Id;
    }

    public async Task<(bool Success, string? Error, int? ConversationId)> CreateGroupAsync(string creatorUserId, string name, IEnumerable<string> memberUserIds, IReadOnlyCollection<string> creatorRoles)
    {
        var cleanName = (name ?? string.Empty).Trim();
        if (cleanName.Length < 2 || cleanName.Length > 120)
            return (false, "Group name must be between 2 and 120 characters.", null);

        var limit = GetGroupLimit(creatorRoles);
        var activeCreatedGroups = await _db.CsConversations
            .CountAsync(c => c.IsGroup && !c.IsArchived && c.CreatedByUserId == creatorUserId);

        if (activeCreatedGroups >= limit)
            return (false, $"You reached your group limit ({limit}).", null);

        var uniqueMembers = memberUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(id => id != creatorUserId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (uniqueMembers.Count == 0)
            return (false, "Select at least one member for the group.", null);

        var eligible = await GetEligibleUsersAsync(creatorUserId);
        var eligibleSet = eligible.Select(u => u.UserId).ToHashSet(StringComparer.Ordinal);
        if (uniqueMembers.Any(id => !eligibleSet.Contains(id)))
            return (false, "One or more selected members are not eligible for CS messaging.", null);

        var now = DateTime.UtcNow;
        var conversation = new CsConversation
        {
            Name = cleanName,
            IsGroup = true,
            CreatedByUserId = creatorUserId,
            CreatedAt = now,
            UpdatedAt = now,
            Members =
            [
                new CsConversationMember { UserId = creatorUserId, JoinedAt = now, IsActive = true, LastReadAt = now },
                .. uniqueMembers.Select(id => new CsConversationMember { UserId = id, JoinedAt = now, IsActive = true })
            ]
        };

        _db.CsConversations.Add(conversation);
        await _db.SaveChangesAsync();
        return (true, null, conversation.Id);
    }

    public async Task<(bool Success, string? Error, CsMessageViewModel? Message)> AddMessageAsync(int conversationId, string userId, string body, string? gifUrl)
    {
        var member = await _db.CsConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.IsActive);
        if (member is null)
            return (false, "You are not a member of this conversation.", null);

        var cleanBody = (body ?? string.Empty).Trim();
        var cleanGif = NormalizeGifUrl(gifUrl);

        if (string.IsNullOrWhiteSpace(cleanBody) && string.IsNullOrWhiteSpace(cleanGif))
            return (false, "Message cannot be empty.", null);

        if (cleanBody.Length > 5000)
            return (false, "Message exceeds 5000 characters.", null);

        if (!string.IsNullOrWhiteSpace(cleanGif) && cleanGif.Length > 1000)
            return (false, "GIF URL is too long.", null);

        var message = new CsMessage
        {
            ConversationId = conversationId,
            AuthorUserId = userId,
            Body = cleanBody,
            GifUrl = cleanGif,
            CreatedAt = DateTime.UtcNow
        };

        _db.CsMessages.Add(message);

        var conversation = await _db.CsConversations.FirstAsync(c => c.Id == conversationId);
        conversation.UpdatedAt = DateTime.UtcNow;

        member.LastReadAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var saved = await _db.CsMessages
            .Include(m => m.AuthorUser)
            .Include(m => m.Reactions)
            .FirstAsync(m => m.Id == message.Id);

        var activeMembers = await _db.CsConversationMembers
            .Where(m => m.ConversationId == conversationId && m.IsActive)
            .ToListAsync();

        return (true, null, ToMessageViewModel(saved, userId, activeMembers));
    }

    public async Task<(bool Success, string? Error, int ConversationId, int MessageId, string Emoji, int Count, bool ReactedByCurrentUser)> ToggleReactionAsync(int messageId, string userId, string emoji)
    {
        var cleanEmoji = (emoji ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanEmoji) || cleanEmoji.Length > 32)
            return (false, "Invalid emoji reaction.", 0, messageId, string.Empty, 0, false);

        var message = await _db.CsMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted);

        if (message is null)
            return (false, "Message not found.", 0, messageId, cleanEmoji, 0, false);

        var isMember = await _db.CsConversationMembers.AnyAsync(m =>
            m.ConversationId == message.ConversationId &&
            m.UserId == userId &&
            m.IsActive);

        if (!isMember)
            return (false, "You are not a member of this conversation.", 0, messageId, cleanEmoji, 0, false);

        var existing = await _db.CsMessageReactions.FirstOrDefaultAsync(r =>
            r.MessageId == messageId &&
            r.UserId == userId &&
            r.Emoji == cleanEmoji);

        var reacted = false;
        if (existing is null)
        {
            _db.CsMessageReactions.Add(new CsMessageReaction
            {
                MessageId = messageId,
                UserId = userId,
                Emoji = cleanEmoji,
                CreatedAt = DateTime.UtcNow
            });
            reacted = true;
        }
        else
        {
            _db.CsMessageReactions.Remove(existing);
        }

        await _db.SaveChangesAsync();

        var count = await _db.CsMessageReactions.CountAsync(r => r.MessageId == messageId && r.Emoji == cleanEmoji);
        return (true, null, message.ConversationId, messageId, cleanEmoji, count, reacted);
    }

    public async Task<bool> MarkReadAsync(int conversationId, string userId)
    {
        var member = await _db.CsConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.IsActive);
        if (member is null) return false;

        member.LastReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error, CsMessageViewModel? Message)> EditMessageAsync(int messageId, string userId, string newBody)
    {
        var cleanBody = (newBody ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanBody))
            return (false, "Message body cannot be empty.", null);

        if (cleanBody.Length > 5000)
            return (false, "Message exceeds 5000 characters.", null);

        var message = await _db.CsMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted);

        if (message is null)
            return (false, "Message not found.", null);

        if (!string.Equals(message.AuthorUserId, userId, StringComparison.Ordinal))
            return (false, "You can only edit your own messages.", null);

        var editWindow = TimeSpan.FromMinutes(15);
        if (DateTime.UtcNow - message.CreatedAt > editWindow)
            return (false, "The edit window (15 min) has passed.", null);

        message.Body = cleanBody;
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var saved = await _db.CsMessages
            .Include(m => m.AuthorUser)
            .Include(m => m.Reactions)
            .FirstAsync(m => m.Id == messageId);

        var activeMembers = await _db.CsConversationMembers
            .Where(m => m.ConversationId == saved.ConversationId && m.IsActive)
            .ToListAsync();

        return (true, null, ToMessageViewModel(saved, userId, activeMembers));
    }

    public async Task<(bool Success, string? Error, int ConversationId)> DeleteMessageAsync(int messageId, string userId)
    {
        var message = await _db.CsMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted);

        if (message is null)
            return (false, "Message not found.", 0);

        if (!string.Equals(message.AuthorUserId, userId, StringComparison.Ordinal))
            return (false, "You can only delete your own messages.", 0);

        message.IsDeleted = true;
        message.Body = string.Empty;
        message.GifUrl = null;

        await _db.SaveChangesAsync();
        return (true, null, message.ConversationId);
    }

    public async Task<(bool Success, string? Error, CsConversationMemberViewModel? Member)> AddMemberAsync(int conversationId, string requestingUserId, string targetUserId)
    {
        var conversation = await _db.CsConversations
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == conversationId && !c.IsArchived && c.IsGroup);

        if (conversation is null)
            return (false, "Group conversation not found.", null);

        if (!string.Equals(conversation.CreatedByUserId, requestingUserId, StringComparison.Ordinal))
            return (false, "Only the group creator can add members.", null);

        var eligible = await GetEligibleUsersAsync(requestingUserId);
        if (!eligible.Any(u => u.UserId == targetUserId))
            return (false, "User is not eligible for CS messaging.", null);

        var existing = conversation.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (existing is not null)
        {
            if (existing.IsActive)
                return (false, "User is already a member.", null);

            existing.IsActive = true;
            existing.JoinedAt = DateTime.UtcNow;
        }
        else
        {
            _db.CsConversationMembers.Add(new CsConversationMember
            {
                ConversationId = conversationId,
                UserId = targetUserId,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        await _db.SaveChangesAsync();

        var user = await _users.FindByIdAsync(targetUserId);
        return (true, null, new CsConversationMemberViewModel
        {
            UserId = targetUserId,
            DisplayName = user?.DisplayName ?? user?.UserName ?? targetUserId
        });
    }

    public async Task<(bool Success, string? Error)> RemoveMemberAsync(int conversationId, string requestingUserId, string targetUserId)
    {
        var conversation = await _db.CsConversations
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == conversationId && !c.IsArchived && c.IsGroup);

        if (conversation is null)
            return (false, "Group conversation not found.");

        if (!string.Equals(conversation.CreatedByUserId, requestingUserId, StringComparison.Ordinal))
            return (false, "Only the group creator can remove members.");

        if (string.Equals(targetUserId, requestingUserId, StringComparison.Ordinal))
            return (false, "You cannot remove yourself from a group you created.");

        var member = conversation.Members.FirstOrDefault(m => m.UserId == targetUserId && m.IsActive);
        if (member is null)
            return (false, "User is not an active member of this group.");

        member.IsActive = false;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<CsRecentConversationViewModel>> GetRecentConversationsAsync(string userId, int take = 8)
    {
        take = Math.Clamp(take, 1, 20);

        var conversations = await _db.CsConversations
            .Where(c => !c.IsArchived)
            .Where(c => c.Members.Any(m => m.UserId == userId && m.IsActive))
            .Include(c => c.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.User)
            .Include(c => c.Messages.OrderByDescending(mm => mm.CreatedAt).Take(1))
            .OrderByDescending(c => c.UpdatedAt)
            .Take(take)
            .ToListAsync();

        var list = new List<CsRecentConversationViewModel>(conversations.Count);

        foreach (var conversation in conversations)
        {
            var lastMessageAt = conversation.Messages
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => (DateTime?)m.CreatedAt)
                .FirstOrDefault();

            var me = conversation.Members.FirstOrDefault(m => m.UserId == userId);
            var unread = 0;
            if (me is not null)
            {
                unread = await _db.CsMessages.CountAsync(m =>
                    m.ConversationId == conversation.Id &&
                    !m.IsDeleted &&
                    m.AuthorUserId != userId &&
                    m.CreatedAt > (me.LastReadAt ?? DateTime.MinValue));
            }

            list.Add(new CsRecentConversationViewModel
            {
                ConversationId = conversation.Id,
                DisplayName = BuildConversationDisplayName(conversation, userId),
                IsGroup = conversation.IsGroup,
                UnreadCount = unread,
                LastMessageAt = lastMessageAt
            });
        }

        return list
            .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
            .ToList();
    }

    private static string BuildConversationDisplayName(CsConversation conversation, string currentUserId)
    {
        if (conversation.IsGroup)
            return string.IsNullOrWhiteSpace(conversation.Name) ? $"Group #{conversation.Id}" : conversation.Name!;

        var other = conversation.Members
            .Where(m => m.IsActive)
            .Select(m => m.User)
            .FirstOrDefault(u => u != null && u.Id != currentUserId);

        return other?.DisplayName ?? other?.UserName ?? "Direct chat";
    }

    private static string? BuildLastMessagePreview(CsMessage? message)
    {
        if (message is null) return null;
        if (message.IsDeleted) return "Message deleted";
        if (!string.IsNullOrWhiteSpace(message.Body))
            return message.Body.Length <= 80 ? message.Body : message.Body[..80] + "…";
        return !string.IsNullOrWhiteSpace(message.GifUrl) ? "GIF" : "Message";
    }

    private static int GetGroupLimit(IReadOnlyCollection<string> roles)
    {
        if (roles.Contains(Roles.BrandManager) || roles.Contains(Roles.SwissArmyKnife)) return 10;
        if (roles.Contains(Roles.TeamLeader)) return 5;
        return 3;
    }

    private static string? NormalizeGifUrl(string? gifUrl)
    {
        if (string.IsNullOrWhiteSpace(gifUrl)) return null;
        var value = gifUrl.Trim();

        // Allow app-relative media URLs (e.g. pasted uploads under /uploads/...)
        if (value.StartsWith('/'))
            return value;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return null;
        return value;
    }

    private static CsMessageViewModel ToMessageViewModel(CsMessage message, string currentUserId, IReadOnlyCollection<CsConversationMember> activeMembers)
    {
        var isMine = string.Equals(message.AuthorUserId, currentUserId, StringComparison.Ordinal);

        var readByAllOthers = activeMembers
            .Where(m => m.IsActive && !string.Equals(m.UserId, message.AuthorUserId, StringComparison.Ordinal))
            .All(m => (m.LastReadAt ?? DateTime.MinValue) >= message.CreatedAt);

        return new CsMessageViewModel
        {
            MessageId = message.Id,
            ConversationId = message.ConversationId,
            AuthorUserId = message.AuthorUserId,
            AuthorName = message.AuthorUser?.DisplayName ?? message.AuthorUser?.UserName ?? message.AuthorUserId,
            Body = message.Body,
            GifUrl = message.GifUrl,
            CreatedAt = message.CreatedAt,
            IsEdited = message.IsEdited,
            IsDeleted = message.IsDeleted,
            IsMine = isMine,
            IsReadByAllOthers = isMine && readByAllOthers,
            Reactions = message.Reactions
                .GroupBy(r => r.Emoji)
                .Select(g => new CsMessageReactionViewModel
                {
                    Emoji = g.Key,
                    Count = g.Count(),
                    ReactedByCurrentUser = g.Any(x => x.UserId == currentUserId)
                })
                .OrderBy(r => r.Emoji)
                .ToList()
        };
    }
}
