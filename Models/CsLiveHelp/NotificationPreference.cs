using System;

namespace Unified.Models.CsLiveHelp
{
    public class NotificationPreference
    {
        public int Id { get; set; }

        /// <summary>
        /// The user ID this preference belongs to
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Context type: "page" (Requests, Board, etc), "request" (specific request), "conversation" (messaging), "group" (messaging group)
        /// </summary>
        public string ContextType { get; set; }

        /// <summary>
        /// The ID of the context (page name, request ID, conversation ID, group ID)
        /// </summary>
        public string ContextId { get; set; }

        /// <summary>
        /// Whether notifications are muted for this context
        /// </summary>
        public bool IsMuted { get; set; }

        /// <summary>
        /// When this preference was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// When this preference was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
