using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Unified.Hubs;
using Unified.Models.CsMessaging;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
public class CsMessagingController : Controller
{
    private readonly CsMessagingService _svc;
    private readonly UserManager<AppUser> _users;
    private readonly IHubContext<CsMessagingHub> _hub;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;

    public CsMessagingController(
        CsMessagingService svc,
        UserManager<AppUser> users,
        IHubContext<CsMessagingHub> hub,
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory)
    {
        _svc = svc;
        _users = users;
        _hub = hub;
        _env = env;
        _httpClientFactory = httpClientFactory;
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
            EligibleUsers = eligibleUsers
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

        await _hub.Clients.Group("cs-messaging").SendAsync("ConversationChanged", new { conversationId = conversationId.Value });
        return Json(new { success = true, conversationId = conversationId.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupInputModel input)
    {
        var creatorId = _users.GetUserId(User)!;
        var roleList = (await _users.GetRolesAsync(await _users.GetUserAsync(User)!)).ToList();

        var result = await _svc.CreateGroupAsync(creatorId, input.Name, input.MemberUserIds, roleList);
        if (!result.Success || !result.ConversationId.HasValue)
            return BadRequest(new { success = false, error = result.Error ?? "Failed to create group." });

        await _hub.Clients.Group("cs-messaging").SendAsync("ConversationChanged", new { conversationId = result.ConversationId.Value });
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
        await _hub.Clients.Group("cs-messaging").SendAsync("ConversationChanged", new { conversationId = id });

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
        await _hub.Clients.Group("cs-messaging").SendAsync("ConversationChanged", new { conversationId });

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> SearchGifs(string q)
    {
        var term = (q ?? string.Empty).Trim();
        if (term.Length < 2)
            return Json(new { success = true, gifs = Array.Empty<object>() });

        var apiKey = "LIVDSRZULELA";
        var url = $"https://g.tenor.com/v1/search?q={Uri.EscapeDataString(term)}&key={apiKey}&limit=20&media_filter=minimal";

        try
        {
            using var client = _httpClientFactory.CreateClient();
            using var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return Json(new { success = false, error = "GIF provider is unavailable right now." });

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var results = new List<object>();
            if (doc.RootElement.TryGetProperty("results", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (!item.TryGetProperty("media", out var media) || media.ValueKind != JsonValueKind.Array || media.GetArrayLength() == 0)
                        continue;

                    var media0 = media[0];
                    var tiny = media0.TryGetProperty("tinygif", out var tinyGif) ? tinyGif : media0.TryGetProperty("gif", out var gif) ? gif : default;
                    if (tiny.ValueKind == JsonValueKind.Undefined) continue;

                    var previewUrl = tiny.TryGetProperty("url", out var purl) ? purl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(previewUrl)) continue;

                    results.Add(new
                    {
                        id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null,
                        previewUrl,
                        title = item.TryGetProperty("title", out var t) ? t.GetString() : "GIF"
                    });
                }
            }

            return Json(new { success = true, gifs = results });
        }
        catch
        {
            return Json(new { success = false, error = "GIF search failed." });
        }
    }

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
