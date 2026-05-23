using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Unified.Models.Identity;

namespace Unified.Hubs;

[Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
public class CsMessagingHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "cs-messaging");
        await base.OnConnectedAsync();
    }

    public Task JoinConversation(int conversationId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"conv-{conversationId}");

    public Task LeaveConversation(int conversationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
}
