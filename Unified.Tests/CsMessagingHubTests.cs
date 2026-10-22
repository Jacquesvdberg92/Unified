using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Unified.Hubs;
using Unified.Models.CsMessaging;
using Unified.Tests.Helpers;

namespace Unified.Tests;

public class CsMessagingHubTests
{
    private static async Task<(CsMessagingHub hub, FakeGroupManager groups, int conversationId, HubConnectionTracker tracker)> BuildAsync(string name, string? callerUserId)
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

        var groups  = new FakeGroupManager();
        var tracker = new HubConnectionTracker();
        var hub = new CsMessagingHub(db, tracker)
        {
            Context = new FakeHubCallerContext("conn-1", callerUserId),
            Groups  = groups
        };

        return (hub, groups, conversation.Id, tracker);
    }

    [Fact]
    public async Task JoinConversation_ActiveMember_JoinsGroup()
    {
        var (hub, groups, conversationId, _) = await BuildAsync(nameof(JoinConversation_ActiveMember_JoinsGroup), "member-1");

        await hub.JoinConversation(conversationId);

        Assert.Contains(("conn-1", $"conv-{conversationId}"), groups.Added);
    }

    [Fact]
    public async Task JoinConversation_NonMember_IsRejected()
    {
        var (hub, groups, conversationId, _) = await BuildAsync(nameof(JoinConversation_NonMember_IsRejected), "outsider");

        await Assert.ThrowsAsync<HubException>(() => hub.JoinConversation(conversationId));

        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task JoinConversation_InactiveMember_IsRejected()
    {
        var (hub, groups, conversationId, _) = await BuildAsync(nameof(JoinConversation_InactiveMember_IsRejected), "former-member");

        await Assert.ThrowsAsync<HubException>(() => hub.JoinConversation(conversationId));

        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task JoinConversation_NoUserId_IsRejected()
    {
        var (hub, groups, conversationId, _) = await BuildAsync(nameof(JoinConversation_NoUserId_IsRejected), null);

        await Assert.ThrowsAsync<HubException>(() => hub.JoinConversation(conversationId));

        Assert.Empty(groups.Added);
    }

    [Fact]
    public async Task OnConnected_TracksConnectionAndJoinsActiveConversations()
    {
        var (hub, groups, conversationId, tracker) = await BuildAsync(nameof(OnConnected_TracksConnectionAndJoinsActiveConversations), "member-1");

        await hub.OnConnectedAsync();

        Assert.Equal(["conn-1"], tracker.GetConnections("member-1"));
        Assert.Contains(("conn-1", "cs-messaging"), groups.Added);
        Assert.Contains(("conn-1", $"conv-{conversationId}"), groups.Added);
    }

    [Fact]
    public async Task OnDisconnected_ForgetsConnection()
    {
        var (hub, _, _, tracker) = await BuildAsync(nameof(OnDisconnected_ForgetsConnection), "member-1");

        await hub.OnConnectedAsync();
        await hub.OnDisconnectedAsync(null);

        Assert.Empty(tracker.GetConnections("member-1"));
    }

    [Fact]
    public void Tracker_KeepsEachUsersConnectionsSeparately()
    {
        var tracker = new HubConnectionTracker();
        tracker.Add("alice", "a1");
        tracker.Add("alice", "a2");
        tracker.Add("bob", "b1");

        tracker.Remove("alice", "a1");
        tracker.Remove("nobody", "x");

        Assert.Equal(["a2"], tracker.GetConnections("alice"));
        Assert.Equal(["b1"], tracker.GetConnections("bob"));
        Assert.Empty(tracker.GetConnections("nobody"));
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
