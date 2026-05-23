using Microsoft.EntityFrameworkCore;
using Unified.Data;

namespace Unified.Services;

/// <summary>
/// Sends messages to a Telegram group via the configured bot.
/// The bot must be added to the target group with admin rights.
/// </summary>
public class TelegramService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramService> _logger;

    public TelegramService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<TelegramService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Sends a "Please log me in" message to the configured Telegram group.
    /// Returns true on success, false if not configured or request failed.
    /// </summary>
    public async Task<(bool success, string? error)> SendLoginRequestAsync(string displayName, string? anydeskId)
    {
        var settings = await _db.TelegramBotSettings.FirstOrDefaultAsync();
        if (settings is null || !settings.IsEnabled)
            return (false, "Telegram integration is not enabled.");

        if (string.IsNullOrWhiteSpace(settings.BotToken) || string.IsNullOrWhiteSpace(settings.ChatId))
            return (false, "Telegram bot is not configured.");

        var anydeskText = string.IsNullOrWhiteSpace(anydeskId)
            ? "<i>(no AnyDesk ID set — ask your admin to add it to your profile)</i>"
            : $"<code>{HtmlEncode(anydeskId)}</code>";

        var text = $"🔐 <b>Login Request</b>\n\n" +
                   $"<b>Agent:</b> {HtmlEncode(displayName)}\n" +
                   $"<b>AnyDesk ID:</b> {anydeskText}\n\n" +
                   $"Please log this agent in.";

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.telegram.org/bot{settings.BotToken}/sendMessage";
            var payload = new
            {
                chat_id = settings.ChatId,
                text,
                parse_mode = "HTML"
            };

            var response = await client.PostAsJsonAsync(url, payload);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Telegram sendMessage failed: {Status} {Body}", response.StatusCode, body);
                return (false, "Telegram API returned an error. Check bot token and chat ID.");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram sendMessage exception");
            return (false, "Failed to contact Telegram API.");
        }
    }

    private static string HtmlEncode(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
