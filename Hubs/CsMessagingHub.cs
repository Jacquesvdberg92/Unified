using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;

namespace Unified.Hubs;

[Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
public class CsMessagingHub : Hub
{
    private readonly AppDbContext _dbContext;

    public CsMessagingHub(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "cs-messaging");

        // Join all conversations for the current user
        await JoinAllUserConversations();

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Automatically join all conversations the user is a member of
    /// This ensures real-time message delivery even if user isn't viewing the conversation
    /// </summary>
    public async Task JoinAllUserConversations()
    {
        try {
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return;

            // Get all active conversations for this user
            var conversationIds = await _dbContext.CsConversationMembers
                .Where(m => m.UserId == userId && m.IsActive)
                .Select(m => m.ConversationId)
                .ToListAsync();

            // Join each conversation group
            foreach (var convId in conversationIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"conv-{convId}");
            }

            System.Diagnostics.Debug.WriteLine($"[CsMessagingHub] User {userId} joined {conversationIds.Count} conversation groups");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CsMessagingHub] Error joining conversations: {ex.Message}");
        }
    }

    public Task JoinConversation(int conversationId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"conv-{conversationId}");

    public Task LeaveConversation(int conversationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv-{conversationId}");

    /// <summary>
    /// Broadcast new message notification with metadata.
    /// </summary>
    public async Task NotifyNewMessage(int conversationId, string author, string preview, bool isGroupMessage = false)
    {
        await Clients.Group($"conv-{conversationId}").SendAsync("MessageNotification", new
        {
            type = isGroupMessage ? "groupMessage" : "directMessage",
            conversationId,
            author,
            preview,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast mention notification with metadata.
    /// </summary>
    public async Task NotifyMention(int conversationId, string author, string mentionedUser, string messagePreview)
    {
        await Clients.Group($"conv-{conversationId}").SendAsync("MentionNotification", new
        {
            type = "mention",
            conversationId,
            author,
            mentionedUser,
            messagePreview,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast direct message notification.
    /// </summary>
    public async Task NotifyDirectMessage(string recipientUserId, string senderName, string messagePreview)
    {
        await Clients.User(recipientUserId).SendAsync("DirectMessageNotification", new
        {
            type = "directMessage",
            sender = senderName,
            preview = messagePreview,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast group message notification to all members except sender.
    /// </summary>
    public async Task NotifyGroupMessage(int conversationId, string senderUserId, string senderName, string messagePreview)
    {
        await Clients.GroupExcept($"conv-{conversationId}", new[] { Context.ConnectionId })
            .SendAsync("GroupMessageNotification", new
            {
                type = "groupMessage",
                conversationId,
                sender = senderName,
                preview = messagePreview,
                timestamp = DateTime.UtcNow
            });
    }
}
