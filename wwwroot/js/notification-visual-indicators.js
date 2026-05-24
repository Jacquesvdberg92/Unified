/**
 * Notification Visual Indicators
 * Enhanced visual feedback for notifications (badges, highlights, titles)
 */

const NotificationVisualIndicators = (() => {
    const NOTIFICATION_COLOR_MAP = {
        'newRequest': '#0d6efd',      // blue
        'comment': '#198754',          // green
        'mention': '#fd7e14',          // orange
        'escalated': '#dc3545',        // red
        'directMessage': '#6f42c1',    // purple
        'groupMessage': '#0dcaf0',     // cyan
        'internalComment': '#ffc107'   // yellow
    };

    /**
     * Update page title with notification badge
     */
    function updatePageTitle(contextType) {
        const stats = NotificationManager?.getStats?.();
        if (!stats || stats.totalRecent === 0) {
            document.title = document.title.replace(/^\(\d+\)\s*/, '');
            return;
        }

        const count = stats.byContext[contextType] || 0;
        if (count > 0) {
            const prefix = `(${count}) `;
            if (!document.title.startsWith(prefix)) {
                document.title = prefix + document.title;
            }
        }
    }

    /**
     * Create a badge element for visual indicator
     */
    function createNotificationBadge(notificationType = 'notification', count = 1) {
        const badge = document.createElement('span');
        badge.className = 'badge notification-badge';
        badge.style.backgroundColor = NOTIFICATION_COLOR_MAP[notificationType] || '#0d6efd';
        badge.textContent = count > 99 ? '99+' : count;
        return badge;
    }

    /**
     * Add notification indicator to element
     */
    function addIndicatorToElement(element, notificationType = 'notification') {
        if (!element) return;

        const existingBadge = element.querySelector('.notification-badge');
        if (existingBadge) {
            const count = parseInt(existingBadge.textContent, 10) || 1;
            existingBadge.textContent = count + 1;
        } else {
            const badge = createNotificationBadge(notificationType);
            element.style.position = 'relative';
            element.appendChild(badge);
        }
    }

    /**
     * Create a toast notification element
     */
    function createToastElement(title, message, notificationType = 'notification') {
        const toastId = `toast-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
        const bgColor = notificationType === 'mention' ? 'bg-warning' :
                       notificationType === 'comment' ? 'bg-info' :
                       notificationType === 'escalated' ? 'bg-danger' :
                       'bg-primary';

        const toast = document.createElement('div');
        toast.id = toastId;
        toast.className = `toast notification-toast ${bgColor}`;
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');
        toast.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            min-width: 300px;
            z-index: 1050;
            color: white;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        `;

        toast.innerHTML = `
            <div class="toast-header ${bgColor} text-white">
                <i class="ri-notification-3-line me-2"></i>
                <strong class="me-auto">${escapeHtml(title)}</strong>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
            <div class="toast-body">
                ${escapeHtml(message)}
            </div>
        `;

        return toast;
    }

    /**
     * Show a toast notification
     */
    function showToast(title, message, notificationType = 'notification') {
        const toast = createToastElement(title, message, notificationType);
        document.body.appendChild(toast);

        // Bootstrap toast initialization
        try {
            const bsToast = new bootstrap.Toast(toast, {
                autohide: true,
                delay: 4000
            });
            bsToast.show();

            // Remove from DOM after it's hidden
            toast.addEventListener('hidden.bs.toast', () => {
                toast.remove();
            });
        } catch (error) {
            console.log('Toast notification:', title, message);
            // Fallback: just remove after timeout
            setTimeout(() => toast.remove(), 4500);
        }
    }

    /**
     * Highlight an element with glow effect
     */
    function highlightElement(element) {
        if (!element) return;

        element.classList.add('notification-glow');
        setTimeout(() => {
            element.classList.remove('notification-glow');
        }, 3000);
    }

    /**
     * Update icon with animation
     */
    function animateIcon(element, animationClass = 'pulse') {
        if (!element) return;

        element.classList.add(animationClass);
        element.addEventListener('animationend', () => {
            element.classList.remove(animationClass);
        }, { once: true });
    }

    /**
     * Create a notification summary card
     */
    function createSummaryCard(stats) {
        if (!stats || stats.totalRecent === 0) return null;

        const card = document.createElement('div');
        card.className = 'card border-primary mt-2';
        card.style.cssText = 'background-color: #f8f9ff;';

        let content = '<div class="card-body py-2">';
        content += '<strong class="small">Recent Notifications</strong><br>';

        Object.entries(stats.byContext).forEach(([context, count]) => {
            const icon = context === 'Requests' ? 'ri-file-list-line' :
                        context === 'Board' ? 'ri-kanban-view' :
                        context === 'CsMessaging' ? 'ri-chat-3-line' :
                        'ri-notification-3-line';
            content += `<small class="d-block mt-1"><i class="${icon} me-1"></i>${context}: ${count}</small>`;
        });

        content += '</div>';
        card.innerHTML = content;

        return card;
    }

    /**
     * Escape HTML to prevent XSS
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Clear all visual indicators for a context
     */
    function clearIndicators(contextType, contextId = '') {
        const selector = contextId
            ? `[data-notification-context="${contextType}"][data-notification-id="${contextId}"] .notification-badge`
            : `[data-notification-context="${contextType}"] .notification-badge`;

        document.querySelectorAll(selector).forEach(badge => {
            badge.remove();
        });

        updatePageTitle(contextType);
    }

    /**
     * Get notification type icon
     */
    function getNotificationIcon(notificationType) {
        return notificationType === 'comment' ? 'ri-chat-3-line' :
               notificationType === 'mention' ? 'ri-at-line' :
               notificationType === 'escalated' ? 'ri-alert-line' :
               notificationType === 'directMessage' ? 'ri-mail-line' :
               notificationType === 'groupMessage' ? 'ri-group-2-line' :
               'ri-notification-3-line';
    }

    /**
     * Auto-update title with notification count
     */
    function autoUpdateTitle(contextType = 'Board') {
        setInterval(() => {
            updatePageTitle(contextType);
        }, 2000);
    }

    // Public API
    return {
        updatePageTitle,
        createNotificationBadge,
        addIndicatorToElement,
        createToastElement,
        showToast,
        highlightElement,
        animateIcon,
        createSummaryCard,
        clearIndicators,
        getNotificationIcon,
        autoUpdateTitle,
        NOTIFICATION_COLOR_MAP
    };
})();

// Auto-initialize visual indicators when NotificationManager triggers events
document.addEventListener('notificationPreferenceChanged', () => {
    if (typeof NotificationManager !== 'undefined') {
        const pageContext = document.body.dataset.pageContext || 'Board';
        NotificationVisualIndicators.updatePageTitle(pageContext);
    }
});
