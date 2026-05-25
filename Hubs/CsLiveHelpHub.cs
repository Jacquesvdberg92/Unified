using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Unified.Models.Identity;

namespace Unified.Hubs;

/// <summary>
/// SignalR hub for real-time CS Live Help board updates.
/// 
/// ARCHITECTURE REFERENCE: See docs/CsLiveHelp-Architecture.md for:
/// - Group membership and event routing
/// - SignalR event flow between controller, hub, and client
/// - Real-time update behavior on all three pages
///
/// Groups:
///   "cs-board"     — joined by every CS agent / TL / Manager / BrandManager / SAK (receives all public updates)
///   "am-{userId}"  — joined by individual AM; receives only their own card events
///
/// Events pushed by the server (via IHubContext&lt;CsLiveHelpHub&gt;):
///   CardAdded          { id, brandName, requestType, status, assignedTo, isInternal }
///   CardUpdated        { id, brandName, requestType, status, assignedTo }
///   CardStatusChanged  { id, newStatus, assignedTo }  — includes assignment when card is moved
///   CardDeleted        { id }
///   CommentAdded       { requestId, author, body, isSystem, createdAt }  — NOT sent if IsCsInternalOnly
///   BroadcastBanner    { message, actor, timestamp }
/// </summary>
[Authorize(Roles = $"{Roles.AccountManager},{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
public class CsLiveHelpHub : Hub
{
    /// <summary>
    /// Called when a client connects.
    /// CS roles join the shared "cs-board" group; AMs join their private "am-{userId}" group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user is null) { await base.OnConnectedAsync(); return; }

        if (user.IsInRole(Roles.CSAgent)      ||
            user.IsInRole(Roles.TeamLeader)   ||
            user.IsInRole(Roles.BrandManager) ||
            user.IsInRole(Roles.SwissArmyKnife))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "cs-board");
        }
        else if (user.IsInRole(Roles.AccountManager))
        {
            var userId = Context.UserIdentifier ?? Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"am-{userId}");
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Broadcast notification with metadata for sound and visual indicators.
    /// Used by controllers to trigger client-side notifications.
    /// </summary>
    public async Task NotifyNewRequest(string requestId, string brandName, string requestType, string actorName)
    {
        await Clients.Group("cs-board").SendAsync("RequestNotification", new
        {
            type = "newRequest",
            requestId,
            brandName,
            requestType,
            actor = actorName,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast comment notification with metadata.
    /// </summary>
    public async Task NotifyCommentAdded(string requestId, string author, string contextType = "Board")
    {
        // Send to CS board for comment on picked requests
        await Clients.Group("cs-board").SendAsync("CommentNotification", new
        {
            type = "comment",
            requestId,
            author,
            contextType,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast comment notification to specific AM (for their own requests).
    /// </summary>
    public async Task NotifyCommentToAm(string userId, string requestId, string author)
    {
        await Clients.Group($"am-{userId}").SendAsync("CommentNotification", new
        {
            type = "comment",
            requestId,
            author,
            contextType = "Requests",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast request escalation notification.
    /// </summary>
    public async Task NotifyRequestEscalated(string requestId, string brandName, string actor)
    {
        await Clients.Group("cs-board").SendAsync("RequestNotification", new
        {
            type = "escalated",
            requestId,
            brandName,
            actor,
            contextType = "RequestsAllBrands",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast internal-only comment notification (for internal teams).
    /// </summary>
    public async Task NotifyInternalComment(string requestId, string author, string teamId = null)
    {
        await Clients.Group("cs-board").SendAsync("CommentNotification", new
        {
            type = "internalComment",
            requestId,
            author,
            teamId,
            contextType = "RequestsAllBrands",
            timestamp = DateTime.UtcNow
        });
    }
}
