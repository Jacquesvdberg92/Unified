using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Unified.Hubs;
using Unified.Models.CsMessaging;
using Unified.Tests.Helpers;

namespace Unified.Tests;

public class CsMessagingHubTests
{
    private static async Task<(CsMessagingHub hub, FakeGroupManager groups, int conversationId)> BuildAsync(string name, string? callerUserId)
    {
        var db = DbHelper.CreateInMemory(name);

        var conversation = new CsConversation
        {
            CreatedByUserId = "member-1",
            Members =
            {
                new CsConversationMember { UserId = "member-1", IsActive = true },
                new CsConversationMember { UserId = "former-member", IsActive = false }
            }
        };
        db.CsConversations.Add(conversation);
        await db.SaveChangesAsync();

        var groups = new FakeGroupManager();
        var hub = new CsMessagingHub(db)
        {
            Context = new FakeHubCallerContext("conn-1", callerUserId),
            Groups  = groups
        };

        return (hub, groups, conversation.Id);
    }

    [Fact]
    public async Task JoinConversation_ActiveMember_JoinsGroup()
    {
        var (hub, groups, conversationId) = await BuildAsync(nameof(JoinConversation_ActiveMember_JoinsGroup), "member-1");

        await hub.JoinConversation(conversationId);

        Assert.Contains(("conn-1", $"conv-{conversationId}"), groups.Added);
    }

    [Fact]
    public async Task JoinConversation_NonMember_IsRejected()
    {
        var (hub, groups, conversationId) = await BuildAsync(nameof(JoinConversation_NonMember_IsRejected), "outsider");

        await Assert.ThrowsAsync<HubException>(() => hub.JoinConversation(conversationId));

        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task JoinConversation_InactiveMember_IsRejected()
    {
        var (hub, groups, conversationId) = await BuildAsync(nameof(JoinConversation_InactiveMember_IsRejected), "former-member");

        await Assert.ThrowsAsync<HubException>(() => hub.JoinConversation(conversationId));

        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task JoinConversation_NoUserId_IsRejected()
    {
        var (hub, groups, conversationId) = await BuildAsync(nameof(JoinConversation_NoUserId_IsRejected), null);

        await Assert.ThrowsAsync<HubException>(() => hub.JoinConversation(conversationId));

        Assert.Empty(groups.Added);
    }

    private sealed class FakeGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> Added { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeHubCallerContext : HubCallerContext
    {
        public FakeHubCallerContext(string connectionId, string? userId)
        {
            ConnectionId = connectionId;
            UserIdentifier = userId;
            var claims = userId is null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier { get; }
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
