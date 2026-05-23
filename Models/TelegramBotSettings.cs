namespace Unified.Models;

using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Stores the Telegram bot configuration. Only one row is expected (Id = 1).
/// Configure via Admin → Telegram Settings.
/// </summary>
public class TelegramBotSettings
{
    /// <summary>Always 1 — singleton row.</summary>
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; } = 1;

    /// <summary>
    /// Bot API token obtained from @BotFather on Telegram.
    /// The bot must be added to the target group with <strong>admin rights</strong> so it can post messages.
    /// </summary>
    public string? BotToken { get; set; }

    /// <summary>
    /// Numeric chat ID of the Telegram group the bot should post to.
    /// Obtain it by adding @RawDataBot or @userinfobot to the group, or via the getUpdates API call.
    /// </summary>
    public string? ChatId { get; set; }

    /// <summary>Whether the Telegram integration is enabled.</summary>
    public bool IsEnabled { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
