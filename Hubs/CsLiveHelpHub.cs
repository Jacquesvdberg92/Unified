using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Unified.Models.Identity;

namespace Unified.Hubs;

/// <summary>
/// SignalR hub for real-time CS Live Help board updates.
///
/// Groups:
///   "cs-board"     — joined by every CS agent / TL / Manager / BrandManager / SAK
///   "am-{userId}"  — joined by the individual AM so they only see their own card events
///
/// Events pushed by the server (via IHubContext&lt;CsLiveHelpHub&gt;):
///   CardAdded          { id, brandName, requestType, status, assignedTo, isInternal }
///   CardUpdated        { id, brandName, requestType, status, assignedTo }
///   CardStatusChanged  { id, newStatus, assignedTo }
///   CardDeleted        { id }
///   CommentAdded       { requestId, author, body, isSystem, createdAt }
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
}
