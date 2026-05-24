using System;

namespace Unified.Models.CsLiveHelp
{
    public class UserNotificationSettings
    {
        public int Id { get; set; }

        /// <summary>
        /// The user ID this settings record belongs to
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Enable/disable audio notifications globally
        /// </summary>
        public bool AudioNotificationsEnabled { get; set; } = true;

        /// <summary>
        /// Volume level for audio notifications (0-100)
        /// </summary>
        public int AudioVolume { get; set; } = 50;

        /// <summary>
        /// Enable/disable desktop notifications
        /// </summary>
        public bool DesktopNotificationsEnabled { get; set; } = true;

        /// <summary>
        /// Enable/disable toast notifications
        /// </summary>
        public bool ToastNotificationsEnabled { get; set; } = true;

        /// <summary>
        /// Enable/disable badge notifications (unread count)
        /// </summary>
        public bool BadgeNotificationsEnabled { get; set; } = true;

        /// <summary>
        /// Enable/disable notifications for new messages
        /// </summary>
        public bool NotifyOnMessages { get; set; } = true;

        /// <summary>
        /// Enable/disable notifications for mentions
        /// </summary>
        public bool NotifyOnMentions { get; set; } = true;

        /// <summary>
        /// Enable/disable notifications for system alerts
        /// </summary>
        public bool NotifyOnSystemAlerts { get; set; } = true;

        /// <summary>
        /// When this settings record was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this settings record was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
