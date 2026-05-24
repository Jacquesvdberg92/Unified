/**
 * Notification Manager
 * Orchestrates notification sounds, mute preferences, and visual indicators
 * Works with localStorage for client-side mute state management
 */

const NotificationManager = (() => {
    const STORAGE_KEY = 'notificationPreferences';
    const MUTED_BY_DEFAULT = {
        Requests: false,
        Board: false,
        RequestsAllBrands: false,
        CsMessaging: false
    };

    let preferences = loadPreferences();
    let notificationQueue = [];
    let recentNotifications = [];
    let userSettings = null;
    const RECENT_NOTIFICATION_TIMEOUT = 30 * 60 * 1000; // Keep for 30 minutes

    /**
     * Load user notification settings from server
     */
    async function loadUserSettings() {
        if (userSettings) return userSettings; // Already loaded

        try {
            const response = await fetch('/api/notification-preferences/user-settings', {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                console.warn('Failed to load user settings:', response.status);
                return null;
            }

            const result = await response.json();
            if (result.success && result.data) {
                userSettings = result.data;
                console.log('[NotificationManager] Loaded user settings:', userSettings);
                return userSettings;
            }
        } catch (error) {
            console.warn('[NotificationManager] Error loading settings:', error);
        }
        return null;
    }

    /**
     * Load preferences from localStorage or initialize with defaults
     */
    function loadPreferences() {
        try {
            const stored = localStorage.getItem(STORAGE_KEY);
            return stored ? JSON.parse(stored) : {};
        } catch (error) {
            console.error('Error loading notification preferences:', error);
            return {};
        }
    }

    /**
     * Save preferences to localStorage
     */
    function savePreferences() {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(preferences));
        } catch (error) {
            console.error('Error saving notification preferences:', error);
        }
    }

    /**
     * Build preference key from context
     */
    function buildPreferenceKey(contextType, contextId = '') {
        return contextId ? `${contextType}:${contextId}` : contextType;
    }

    /**
     * Check if notifications are muted for a context
     */
    function isMuted(contextType, contextId = '') {
        const key = buildPreferenceKey(contextType, contextId);
        return preferences[key] === true;
    }

    /**
     * Check if notifications are enabled for a context
     */
    function isEnabled(contextType, contextId = '') {
        return !isMuted(contextType, contextId);
    }

    /**
     * Mute notifications for a context
     */
    function mute(contextType, contextId = '') {
        const key = buildPreferenceKey(contextType, contextId);
        preferences[key] = true;
        savePreferences();
        triggerPreferenceChangeEvent(contextType, contextId, true);
        console.log(`Notifications muted for: ${key}`);

        // Async sync to server - don't wait for response
        syncToServer(contextType, contextId).catch(err => 
            console.warn('Failed to sync mute preference to server:', err)
        );
    }

    /**
     * Unmute notifications for a context
     */
    function unmute(contextType, contextId = '') {
        const key = buildPreferenceKey(contextType, contextId);
        delete preferences[key];
        savePreferences();
        triggerPreferenceChangeEvent(contextType, contextId, false);
        console.log(`Notifications unmuted for: ${key}`);

        // Async sync to server - don't wait for response
        syncToServer(contextType, contextId).catch(err => 
            console.warn('Failed to sync unmute preference to server:', err)
        );
    }

    /**
     * Toggle mute status
     */
    function toggleMute(contextType, contextId = '') {
        if (isMuted(contextType, contextId)) {
            unmute(contextType, contextId);
            return false;
        } else {
            mute(contextType, contextId);
            return true;
        }
    }

    /**
     * Handle notification trigger with all logic
     */
    async function handleNotification(options) {
        const {
            type = 'notification', // 'comment', 'mention', 'newMessage', etc
            contextType = 'generic', // 'Requests', 'Board', 'RequestsAllBrands', 'CsMessaging'
            contextId = '', // specific request ID, conversation ID, etc
            title = 'Notification',
            message = 'You have a new notification',
            sound = true,
            visual = true,
            toast = true,
            volume = undefined,
            callback = null
        } = options;

        // Load user settings if not already loaded
        if (!userSettings) {
            await loadUserSettings();
        }

        // Check user notification type preferences
        if (userSettings) {
            if (type === 'newMessage' && !userSettings.notifyOnMessages) {
                console.log('Notifications disabled for messages');
                return false;
            }
            if (type === 'mention' && !userSettings.notifyOnMentions) {
                console.log('Notifications disabled for mentions');
                return false;
            }
            if (type === 'systemAlert' && !userSettings.notifyOnSystemAlerts) {
                console.log('Notifications disabled for system alerts');
                return false;
            }
        }

        const key = buildPreferenceKey(contextType, contextId);

        // Check if muted
        if (isMuted(contextType, contextId)) {
            console.log(`Notification muted for ${key}`);
            return false;
        }

        // Track recent notification
        addToRecentNotifications({
            type,
            contextType,
            contextId,
            title,
            message,
            timestamp: Date.now()
        });

        let playedSound = false;

        // Play sound if enabled in user settings and globally
        if (sound && NotificationSounds.isAudioSupported() && (!userSettings || userSettings.audioNotificationsEnabled)) {
            try {
                // Use user's audio volume if available, otherwise default
                const effectiveVolume = volume !== undefined 
                    ? volume 
                    : (userSettings?.audioVolume !== undefined 
                        ? userSettings.audioVolume / 100 
                        : NotificationSounds.getVolume());

                playedSound = await NotificationSounds.notify(type, {
                    volume: effectiveVolume
                });
            } catch (error) {
                console.error('Error playing notification sound:', error);
            }
        }

        // Show visual indicators if enabled
        if (visual && (!userSettings || userSettings.desktopNotificationsEnabled)) {
            showVisualIndicators(contextType, contextId, title);
        }

        // Show toast notification if enabled
        if (toast && (!userSettings || userSettings.toastNotificationsEnabled)) {
            showToast(title, message, contextType);
        }

        // Update bell icon if badges are enabled
        if (!userSettings || userSettings.badgeNotificationsEnabled) {
            updateNotificationBell();
        }

        // Call callback if provided
        if (typeof callback === 'function') {
            callback({ playedSound, contextType, contextId });
        }

        return true;
    }

    /**
     * Add notification to recent notifications tracking
     */
    function addToRecentNotifications(notification) {
        recentNotifications.push(notification);

        // Keep latest notifications first and cap list for header rendering
        recentNotifications = recentNotifications
            .sort((a, b) => (b.timestamp || 0) - (a.timestamp || 0))
            .slice(0, 20);

        // Clean up old notifications
        const now = Date.now();
        recentNotifications = recentNotifications.filter(notif => 
            now - notif.timestamp < RECENT_NOTIFICATION_TIMEOUT
        );
    }

    /**
     * Get recent notifications
     */
    function getRecentNotifications(contextType = null) {
        if (!contextType) return recentNotifications;
        return recentNotifications.filter(n => n.contextType === contextType);
    }

    /**
     * Show visual indicators on page elements
     */
    function showVisualIndicators(contextType, contextId, title) {
        try {
            // Find and highlight the relevant card/element
            if (contextId) {
                const selector = `[data-notification-context="${contextType}"][data-notification-id="${contextId}"]`;
                const element = document.querySelector(selector);
                if (element) {
                    addGlowEffect(element);
                    updateElementBadge(element);
                }
            }

            // Update page indicator
            updatePageIndicator(contextType);
        } catch (error) {
            console.error('Error showing visual indicators:', error);
        }
    }

    /**
     * Add glow effect to element
     */
    function addGlowEffect(element) {
        element.classList.add('notification-glow');

        // Remove glow after animation
        setTimeout(() => {
            element.classList.remove('notification-glow');
        }, 3000);
    }

    /**
     * Update badge count on element
     */
    function updateElementBadge(element) {
        let badge = element.querySelector('.notification-badge');
        if (!badge) {
            badge = document.createElement('span');
            badge.className = 'notification-badge badge badge-danger';
            badge.textContent = '1';
            element.appendChild(badge);
        } else {
            const count = parseInt(badge.textContent) || 1;
            badge.textContent = count + 1;
        }
    }

    /**
     * Update page-level notification indicator
     */
    function updatePageIndicator(contextType) {
        const pageIndicator = document.querySelector(`[data-page-notifications="${contextType}"]`);
        if (pageIndicator) {
            pageIndicator.classList.add('has-notifications');
            let badge = pageIndicator.querySelector('.page-notification-badge');
            if (!badge) {
                badge = document.createElement('span');
                badge.className = 'page-notification-badge badge badge-warning';
                badge.textContent = '1';
                pageIndicator.appendChild(badge);
            } else {
                const count = parseInt(badge.textContent) || 1;
                badge.textContent = count + 1;
            }
        }
    }

    function renderHeaderNotificationList() {
        const listEl = document.querySelector('#header-notification-scroll');
        const emptyEl = document.querySelector('#header-notification-empty');
        if (!listEl) return;

        const maxItems = 10;
        const items = recentNotifications
            .slice()
            .sort((a, b) => (b.timestamp || 0) - (a.timestamp || 0))
            .slice(0, maxItems);

        if (!items.length) {
            listEl.innerHTML = '';
            if (emptyEl) emptyEl.classList.remove('d-none');
            return;
        }

        if (emptyEl) emptyEl.classList.add('d-none');

        listEl.innerHTML = items.map(item => {
            const title = item.title || 'Notification';
            const message = item.message || '';
            const context = item.contextType || '';
            const ts = item.timestamp ? new Date(item.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';
            return `
                <li class="dropdown-item">
                    <div class="d-flex align-items-start">
                        <div class="pe-2 lh-1 mt-1">
                            <span class="avatar avatar-md avatar-rounded bg-primary2">
                                <i class="ri-notification-3-line lh-1 fs-16"></i>
                            </span>
                        </div>
                        <div class="flex-grow-1">
                            <p class="mb-0 fw-medium fs-13">${title}</p>
                            ${message ? `<p class="mb-0 text-muted small">${message}</p>` : ''}
                            <div class="d-flex align-items-center gap-2 mt-1">
                                ${context ? `<span class="badge bg-light text-dark border">${context}</span>` : ''}
                                ${ts ? `<span class="text-muted small">${ts}</span>` : ''}
                            </div>
                        </div>
                    </div>
                </li>`;
        }).join('');
    }

    /**
     * Update notification bell in header
     */
    function updateNotificationBell() {
        const bell = document.querySelector('#messageDropdown');
        if (!bell) return;

        const recentCount = recentNotifications.length;
        let bellBadge = bell.querySelector('.header-icon-pulse');

        if (recentCount > 0) {
            if (!bellBadge) {
                bellBadge = document.createElement('span');
                bellBadge.className = 'header-icon-pulse bg-primary2 rounded pulse pulse-secondary';
                bell.appendChild(bellBadge);
            } else {
                bellBadge.classList.remove('d-none');
            }

            const badgeEl = bell.parentElement.querySelector('#notifiation-data');
            if (badgeEl) {
                badgeEl.textContent = `${recentCount} Unread`;
            }

            bell.classList.add('has-notifications');
        } else {
            if (bellBadge) {
                bellBadge.classList.add('d-none');
            }

            const badgeEl = bell.parentElement.querySelector('#notifiation-data');
            if (badgeEl) {
                badgeEl.textContent = 'None';
            }

            bell.classList.remove('has-notifications');
        }

        renderHeaderNotificationList();
    }

    /**
     * Show toast notification
     */
    function showToast(title, message, contextType) {
        try {
            // Use Toastify if available (common library)
            if (typeof Toastify !== 'undefined') {
                Toastify({
                    text: `${title}: ${message}`,
                    duration: 4000,
                    gravity: 'top',
                    position: 'right',
                    backgroundColor: '#0d6efd'
                }).showToast();
            } else {
                console.log(`Toast: ${title} - ${message}`);
            }
        } catch (error) {
            console.error('Error showing toast:', error);
        }
    }

    /**
     * Trigger preference change event for UI updates
     */
    function triggerPreferenceChangeEvent(contextType, contextId, isMuted) {
        const event = new CustomEvent('notificationPreferenceChanged', {
            detail: { contextType, contextId, isMuted }
        });
        document.dispatchEvent(event);
    }

    /**
     * Clear all notifications for a context
     */
    function clearNotifications(contextType, contextId = '') {
        try {
            const selector = contextId 
                ? `[data-notification-context="${contextType}"][data-notification-id="${contextId}"] .notification-badge`
                : `[data-notification-context="${contextType}"] .notification-badge`;

            document.querySelectorAll(selector).forEach(badge => {
                badge.remove();
            });

            // Clear from recent
            if (contextId) {
                recentNotifications = recentNotifications.filter(n =>
                    !(n.contextType === contextType && n.contextId === contextId)
                );
            } else {
                recentNotifications = recentNotifications.filter(n => n.contextType !== contextType);
            }

            updateNotificationBell();
        } catch (error) {
            console.error('Error clearing notifications:', error);
        }
    }

    /**
     * Get statistics on notifications
     */
    function getStats() {
        const contextTypes = {};
        recentNotifications.forEach(notif => {
            contextTypes[notif.contextType] = (contextTypes[notif.contextType] || 0) + 1;
        });

        return {
            totalRecent: recentNotifications.length,
            byContext: contextTypes,
            recentNotifications: [...recentNotifications]
        };
    }

    /**
     * Sync preferences to server API
     */
    async function syncToServer(contextType, contextId = '') {
        try {
            const response = await fetch('/api/notificationpreferences/toggle-mute', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    contextType: contextType,
                    contextId: contextId || undefined
                })
            });

            if (!response.ok) {
                console.error('Failed to sync preference to server:', response.statusText);
                return false;
            }

            const data = await response.json();
            return data.success;
        } catch (error) {
            console.error('Error syncing preferences to server:', error);
            return false;
        }
    }

    /**
     * Load all preferences from server
     */
    async function loadFromServer() {
        try {
            const response = await fetch('/api/notificationpreferences/list', {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                console.error('Failed to load preferences from server:', response.statusText);
                return false;
            }

            const data = await response.json();
            if (data.success && data.data) {
                // Sync server preferences with local storage
                data.data.forEach(pref => {
                    if (pref.isMuted) {
                        const key = buildPreferenceKey(pref.contextType, pref.contextId);
                        preferences[key] = true;
                    }
                });
                savePreferences();
                return true;
            }
            return false;
        } catch (error) {
            console.error('Error loading preferences from server:', error);
            return false;
        }
    }

    /**
     * Batch sync preferences to server
     */
    async function batchSyncToServer(prefsToSync) {
        try {
            const response = await fetch('/api/notificationpreferences/batch-set-mute', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    preferences: prefsToSync
                })
            });

            if (!response.ok) {
                console.error('Failed to batch sync preferences:', response.statusText);
                return false;
            }

            const data = await response.json();
            return data.success;
        } catch (error) {
            console.error('Error batch syncing preferences:', error);
            return false;
        }
    }

    // Public API
    return {
        handleNotification,
        isMuted,
        isEnabled,
        mute,
        unmute,
        toggleMute,
        clearNotifications,
        getRecentNotifications,
        getStats,
        updateNotificationBell,
        syncToServer,
        loadFromServer,
        batchSyncToServer,
        // For debugging
        _loadPreferences: loadPreferences,
        _savePreferences: savePreferences,
        _preferences: () => ({ ...preferences })
    };
})();

// Auto-refresh bell/list rendering periodically to keep UI in sync
setInterval(() => {
    const stats = NotificationManager.getStats();
    if (stats.totalRecent > 0) {
        NotificationManager.updateNotificationBell();
    }
}, 6000);

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => NotificationManager.updateNotificationBell());
} else {
    NotificationManager.updateNotificationBell();
}
