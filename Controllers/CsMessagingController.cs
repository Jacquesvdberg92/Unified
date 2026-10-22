using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Unified.Hubs;
using Unified.Models.CsMessaging;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager}")]
public class CsMessagingController : Controller
{
    private readonly CsMessagingService _svc;
    private readonly UserManager<AppUser> _users;
    private readonly IHubContext<CsMessagingHub> _hub;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HubConnectionTracker _connections;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public const string GiphyHttpClientName = "giphy";

    public CsMessagingController(
        CsMessagingService svc,
        UserManager<AppUser> users,
        IHubContext<CsMessagingHub> hub,
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory,
        HubConnectionTracker connections,
        IConfiguration configuration,
        IMemoryCache cache)
    {
        _svc = svc;
        _users = users;
        _hub = hub;
        _env = env;
        _httpClientFactory = httpClientFactory;
        _connections = connections;
        _configuration = configuration;
        _cache = cache;
    }

    private string? GiphyApiKey => _configuration["Giphy:ApiKey"] is { Length: > 0 } key ? key : null;

    /// <summary>
    /// Tells only the conversation's members (all their open tabs) that its list entry changed,
    /// instead of every chat user, so a message in one chat doesn't make every client refetch.
    /// </summary>
    private async Task NotifyConversationChangedAsync(int conversationId, params string[] extraUserIds)
    {
        var userIds = (await _svc.GetActiveMemberIdsAsync(conversationId))
            .Concat(extraUserIds)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (userIds.Count == 0) return;

        await _hub.Clients.Users(userIds).SendAsync("ConversationChanged", new { conversationId });
    }

    /// <summary>
    /// Adds the members' already-open connections to a new conversation's group, so they get its
    /// messages live without a reload (the hub only joins groups when a connection starts).
    /// </summary>
    private async Task SubscribeMembersAsync(int conversationId)
    {
        foreach (var userId in await _svc.GetActiveMemberIdsAsync(conversationId))
            foreach (var connectionId in _connections.GetConnections(userId))
                await _hub.Groups.AddToGroupAsync(connectionId, $"conv-{conversationId}");
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? conversationId)
    {
        var userId = _users.GetUserId(User)!;
        var conversations = await _svc.GetUserConversationsAsync(userId);

        var activeId = conversationId ?? conversations.FirstOrDefault()?.ConversationId;
        CsConversationDetailViewModel? activeConversation = null;
        if (activeId.HasValue)
        {
            activeConversation = await _svc.GetConversationDetailAsync(activeId.Value, userId);
            if (activeConversation is null)
                activeId = null;
            else
                await _svc.MarkReadAsync(activeId.Value, userId);
        }

        var eligibleUsers = await _svc.GetEligibleUsersAsync(userId);

        var model = new CsMessagingIndexViewModel
        {
            CurrentUserId = userId,
            Conversations = conversations,
            ActiveConversation = activeConversation,
            EligibleUsers = eligibleUsers,
            GifSearchEnabled = GiphyApiKey is not null
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Conversation(int id)
    {
        var userId = _users.GetUserId(User)!;
        var detail = await _svc.GetConversationDetailAsync(id, userId);
        if (detail is null) return NotFound();

        await _svc.MarkReadAsync(id, userId);
        return Json(new { success = true, detail });
    }

    [HttpGet]
    public async Task<IActionResult> Conversations()
    {
        var userId = _users.GetUserId(User)!;
        var list = await _svc.GetUserConversationsAsync(userId);
        return Json(new { success = true, conversations = list });
    }

    [HttpGet]
    public async Task<IActionResult> RecentConversations(int take = 8)
    {
        var userId = _users.GetUserId(User)!;
        var list = await _svc.GetRecentConversationsAsync(userId, take);
        return Json(new { success = true, conversations = list });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartDirect(string userId)
    {
        var currentUserId = _users.GetUserId(User)!;
        var conversationId = await _svc.StartDirectAsync(currentUserId, userId);
        if (!conversationId.HasValue)
            return BadRequest(new { success = false, error = "Failed to start direct conversation." });

        await SubscribeMembersAsync(conversationId.Value);
        await NotifyConversationChangedAsync(conversationId.Value);
        return Json(new { success = true, conversationId = conversationId.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupInputModel input)
    {
        var creator = await _users.GetUserAsync(User);
        if (creator is null) return Unauthorized();
        var creatorId = creator.Id;
        var roleList = (await _users.GetRolesAsync(creator)).ToList();

        var result = await _svc.CreateGroupAsync(creatorId, input.Name, input.MemberUserIds, roleList);
        if (!result.Success || !result.ConversationId.HasValue)
            return BadRequest(new { success = false, error = result.Error ?? "Failed to create group." });

        await SubscribeMembersAsync(result.ConversationId.Value);
        await NotifyConversationChangedAsync(result.ConversationId.Value);
        return Json(new { success = true, conversationId = result.ConversationId.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMessage(int id, [FromBody] AddMessageInputModel? input)
    {
        if (input is null)
            return BadRequest(new { success = false, error = "Invalid message payload." });

        var userId = _users.GetUserId(User)!;
        var result = await _svc.AddMessageAsync(id, userId, input.Body, input.GifUrl);
        if (!result.Success || result.Message is null)
            return BadRequest(new { success = false, error = result.Error ?? "Failed to post message." });

        await _hub.Clients.Group($"conv-{id}").SendAsync("MessageAdded", new
        {
            conversationId = id,
            message = result.Message
        });

        await NotifyConversationChangedAsync(id);

        return Json(new { success = true, message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleReaction(int id, [FromBody] ToggleReactionInputModel input)
    {
        var userId = _users.GetUserId(User)!;
        var result = await _svc.ToggleReactionAsync(id, userId, input.Emoji);

        if (!result.Success)
            return BadRequest(new { success = false, error = result.Error ?? "Failed to update reaction." });

        await _hub.Clients.Group($"conv-{result.ConversationId}").SendAsync("MessageReactionUpdated", new
        {
            conversationId = result.ConversationId,
            messageId = result.MessageId,
            emoji = result.Emoji,
            count = result.Count,
            reactedByCurrentUser = result.ReactedByCurrentUser,
            actorUserId = userId
        });

        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMessage(int id, [FromBody] EditMessageInputModel input)
    {
        var userId = _users.GetUserId(User)!;
        var result = await _svc.EditMessageAsync(id, userId, input.Body);
        if (!result.Success || result.Message is null)
            return BadRequest(new { success = false, error = result.Error ?? "Failed to edit message." });

        await _hub.Clients.Group($"conv-{result.Message.ConversationId}").SendAsync("MessageEdited", new
        {
            conversationId = result.Message.ConversationId,
            message = result.Message
        });

        return Json(new { success = true, message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var userId = _users.GetUserId(User)!;
        var result = await _svc.DeleteMessageAsync(id, userId);
        if (!result.Success)
            return BadRequest(new { success = false, error = result.Error ?? "Failed to delete message." });

        await _hub.Clients.Group($"conv-{result.ConversationId}").SendAsync("MessageDeleted", new
        {
            conversationId = result.ConversationId,
            messageId = id
        });

        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddMemberInputModel input)
    {
        var requestingUserId = _users.GetUserId(User)!;
        var result = await _svc.AddMemberAsync(id, requestingUserId, input.UserId);
        if (!result.Success || result.Member is null)
            return BadRequest(new { success = false, error = result.Error ?? "Failed to add member." });

        // Subscribe the new member's already-open tabs so they get live updates without a reload
        foreach (var connectionId in _connections.GetConnections(input.UserId))
            await _hub.Groups.AddToGroupAsync(connectionId, $"conv-{id}");

        await _hub.Clients.Group($"conv-{id}").SendAsync("MemberAdded", new
        {
            conversationId = id,
            member = result.Member
        });
        await NotifyConversationChangedAsync(id);

        return Json(new { success = true, member = result.Member });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int id, string userId)
    {
        var requestingUserId = _users.GetUserId(User)!;
        var result = await _svc.RemoveMemberAsync(id, requestingUserId, userId);
        if (!result.Success)
            return BadRequest(new { success = false, error = result.Error ?? "Failed to remove member." });

        await _hub.Clients.Group($"conv-{id}").SendAsync("MemberRemoved", new
        {
            conversationId = id,
            userId
        });

        // Unsubscribe the removed member's open tabs, or they would keep receiving the
        // conversation's messages until they reconnect
        foreach (var connectionId in _connections.GetConnections(userId))
            await _hub.Groups.RemoveFromGroupAsync(connectionId, $"conv-{id}");

        // The removed member is included so the group drops out of their lists
        await NotifyConversationChangedAsync(id, userId);

        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int conversationId)
    {
        var userId = _users.GetUserId(User)!;
        var ok = await _svc.MarkReadAsync(conversationId, userId);
        if (!ok) return BadRequest(new { success = false, error = "Conversation not found." });

        await _hub.Clients.Group($"conv-{conversationId}").SendAsync("ConversationReadUpdated", new
        {
            conversationId,
            readerUserId = userId
        });
        // Only the reader's own unread counts changed, so only their tabs need to refresh
        await _hub.Clients.User(userId).SendAsync("ConversationChanged", new { conversationId });

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> SearchGifs(string q)
    {
        var term = (q ?? string.Empty).Trim();
        if (term.Length < 2)
            return Json(new { success = true, gifs = Array.Empty<object>() });
        if (term.Length > 50)
            term = term[..50];

        var apiKey = GiphyApiKey;
        if (apiKey is null)
            return Json(new { success = false, error = "GIF search is not set up. Add a GIPHY API key under Giphy:ApiKey." });

        // Cached so repeated searches don't use up the GIPHY rate limit or wait on the network again
        var cacheKey = "giphy-search:" + term.ToLowerInvariant();
        if (_cache.TryGetValue(cacheKey, out List<GifResult>? cached) && cached is not null)
            return Json(new { success = true, gifs = cached });

        var rating = _configuration["Giphy:Rating"] is { Length: > 0 } r ? r : "g";
        var url = $"https://api.giphy.com/v1/gifs/search?api_key={Uri.EscapeDataString(apiKey)}&q={Uri.EscapeDataString(term)}&limit=20&rating={Uri.EscapeDataString(rating)}&bundle=messaging_non_clips";

        try
        {
            using var client = _httpClientFactory.CreateClient(GiphyHttpClientName);
            using var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return Json(new { success = false, error = "GIF provider is unavailable right now." });

            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var results = new List<GifResult>();
            if (doc.RootElement.TryGetProperty("data", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (!item.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Object)
                        continue;

                    // Small renditions keep the picker and the chat light on slow connections
                    var sendUrl = GetGiphyImageUrl(images, "fixed_width") ?? GetGiphyImageUrl(images, "downsized");
                    if (sendUrl is null) continue;
                    var previewUrl = GetGiphyImageUrl(images, "fixed_width_small") ?? sendUrl;

                    results.Add(new GifResult(
                        item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null,
                        previewUrl,
                        sendUrl,
                        item.TryGetProperty("title", out var t) && t.GetString() is { Length: > 0 } title ? title : "GIF"));
                }
            }

            _cache.Set(cacheKey, results, TimeSpan.FromMinutes(30));
            return Json(new { success = true, gifs = results });
        }
        catch
        {
            return Json(new { success = false, error = "GIF search failed." });
        }
    }

    private static string? GetGiphyImageUrl(JsonElement images, string rendition) =>
        images.TryGetProperty(rendition, out var r) && r.TryGetProperty("url", out var u) && u.GetString() is { Length: > 0 } value
            ? value
            : null;

    private sealed record GifResult(string? Id, string PreviewUrl, string Url, string Title);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadPastedImage(IFormFile image)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { success = false, error = "No image found in clipboard." });

        if (image.Length > 5 * 1024 * 1024)
            return BadRequest(new { success = false, error = "Image is too large (max 5 MB)." });

        var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
        if (!allowed.Contains(ext))
        {
            ext = image.ContentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" :
                  image.ContentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) || image.ContentType.Contains("jpg", StringComparison.OrdinalIgnoreCase) ? ".jpg" :
                  image.ContentType.Contains("webp", StringComparison.OrdinalIgnoreCase) ? ".webp" : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
            return BadRequest(new { success = false, error = "Unsupported image type." });

        var userId = _users.GetUserId(User)!;
        var dateFolder = DateTime.UtcNow.ToString("yyyyMMdd");
        var relDir = Path.Combine("uploads", "cs-messages", "pasted", userId, dateFolder);
        var absDir = Path.Combine(_env.WebRootPath, relDir);
        Directory.CreateDirectory(absDir);

        var fileName = $"paste_{DateTime.UtcNow:HHmmss}_{Guid.NewGuid():N}{ext}";
        var absPath = Path.Combine(absDir, fileName);
        await using (var fs = System.IO.File.Create(absPath))
        {
            await image.CopyToAsync(fs);
        }

        var relPath = "/" + Path.Combine(relDir, fileName).Replace("\\", "/");
        return Json(new { success = true, imageUrl = relPath });
    }
}
