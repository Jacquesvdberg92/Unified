using Unified.Models.CsMessaging;
using Unified.Services;
using Unified.Tests.Helpers;

namespace Unified.Tests;

public class CsMessagingServiceTests
{
    [Theory]
    [InlineData("https://media1.giphy.com/media/abc/200w.gif?cid=1&rid=200w.gif")]
    [InlineData("https://i.giphy.com/abc.gif")]
    [InlineData("/uploads/cs-messages/pasted/user-1/20261022/paste_1.png")]
    public void NormalizeGifUrl_AllowsGiphyAndPastedUploads(string url)
    {
        Assert.Equal(url, CsMessagingService.NormalizeGifUrl(url));
    }

    [Theory]
    [InlineData("https://example.com/tracker.gif")]
    [InlineData("https://giphy.com.evil.example/a.gif")]
    [InlineData("http://media.giphy.com/a.gif")]
    [InlineData("//evil.example/a.gif")]
    [InlineData("/uploads/cs-messages/../../appsettings.json")]
    [InlineData("/somewhere/else.png")]
    [InlineData("javascript:alert(1)")]
    public void NormalizeGifUrl_RejectsOtherUrls(string url)
    {
        Assert.Null(CsMessagingService.NormalizeGifUrl(url));
    }

    [Fact]
    public async Task GetActiveMemberIdsAsync_ReturnsOnlyActiveMembers()
    {
        var db = DbHelper.CreateInMemory(nameof(GetActiveMemberIdsAsync_ReturnsOnlyActiveMembers));
        var conversation = new CsConversation
        {
            CreatedByUserId = "a",
            Members =
            {
                new CsConversationMember { UserId = "a", IsActive = true },
                new CsConversationMember { UserId = "b", IsActive = true },
                new CsConversationMember { UserId = "gone", IsActive = false }
            }
        };
        db.CsConversations.Add(conversation);
        await db.SaveChangesAsync();

        var svc = new CsMessagingService(db, null!);

        var ids = await svc.GetActiveMemberIdsAsync(conversation.Id);

        Assert.Equal(["a", "b"], ids.Order());
    }
}
