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

    /// <summary>
    /// Join a conversation's group. Only active members may join, otherwise any CS user
    /// could subscribe to another conversation's messages.
    /// </summary>
    public async Task JoinConversation(int conversationId)
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            throw new HubException("Not a member of this conversation.");

        var isMember = await _dbContext.CsConversationMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.IsActive);
        if (!isMember)
            throw new HubException("Not a member of this conversation.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
    }

    public Task LeaveConversation(int conversationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
}
