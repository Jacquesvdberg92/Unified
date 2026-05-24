/**
 * Notification Page Controls
 * Provides page-level and group-level mute/unmute controls
 * Works with NotificationManager for persistent preference management
 */

const NotificationPageControls = (() => {
    /**
     * Initialize page-level mute controls
     * Call this on pages that need notification controls (Requests, Board, RequestsAllBrands, CsMessaging)
     */
    function initPageControls(pageContextType) {
        if (typeof NotificationManager === 'undefined') {
            console.warn('NotificationManager not available for page controls');
            return;
        }

        // Find all mute buttons on the page
        const muteButtons = document.querySelectorAll('[data-notification-mute-btn]');

        muteButtons.forEach(btn => {
            if (btn.dataset.notificationMuteBound === 'true') {
                const contextType = btn.dataset.notificationMuteBtn || pageContextType;
                const contextId = btn.dataset.notificationContextId || '';
                const isMuted = NotificationManager.isMuted(contextType, contextId);
                updateMuteButtonState(btn, isMuted);
                return;
            }

            btn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                handleMuteButtonClick(btn, pageContextType);
            });

            btn.dataset.notificationMuteBound = 'true';

            // Initialize button state
            const contextType = btn.dataset.notificationMuteBtn || pageContextType;
            const contextId = btn.dataset.notificationContextId || '';
            const isMuted = NotificationManager.isMuted(contextType, contextId);
            updateMuteButtonState(btn, isMuted);
        });

        // Listen for preference changes from other pages/tabs
        document.addEventListener('notificationPreferenceChanged', (e) => {
            const { contextType, contextId, isMuted } = e.detail;
            updatePageControlsForContext(pageContextType, contextType, contextId, isMuted);
        });
    }

    /**
     * Handle mute button click
     */
    function handleMuteButtonClick(button, fallbackPageContextType = '') {
        const contextType = button.dataset.notificationMuteBtn || fallbackPageContextType;
        if (!contextType) return;

        const contextId = button.dataset.notificationContextId || '';
        const isMuted = NotificationManager.toggleMute(contextType, contextId);
        updateMuteButtonState(button, isMuted);

        const message = isMuted
            ? `Notifications muted for ${contextType}`
            : `Notifications unmuted for ${contextType}`;
        console.log(message);
    }

    /**
     * Update mute button visual state
     */
    function updateMuteButtonState(button, isMuted) {
        if (isMuted) {
            button.classList.add('is-muted');
            button.setAttribute('title', 'Click to unmute notifications');
            button.setAttribute('aria-pressed', 'true');
            // Update icon
            const icon = button.querySelector('i');
            if (icon) {
                icon.className = 'ri-notification-off-line';
            }
        } else {
            button.classList.remove('is-muted');
            button.setAttribute('title', 'Click to mute notifications');
            button.setAttribute('aria-pressed', 'false');
            // Update icon
            const icon = button.querySelector('i');
            if (icon) {
                icon.className = 'ri-notification-2-line';
            }
        }
    }

    /**
     * Update page controls when preferences change in real-time
     */
    function updatePageControlsForContext(pageContextType, changedContextType, changedContextId, isMuted) {
        if (changedContextType !== pageContextType) return;

        // Find and update relevant buttons
        const relevantButtons = document.querySelectorAll(
            `[data-notification-mute-btn="${changedContextType}"]` +
            (changedContextId ? `[data-notification-context-id="${changedContextId}"]` : ':not([data-notification-context-id])')
        );

        relevantButtons.forEach(btn => {
            updateMuteButtonState(btn, isMuted);
        });
    }

    function updateAllControlsForContext(changedContextType, changedContextId, isMuted) {
        if (!changedContextType) return;

        const selector = `[data-notification-mute-btn="${changedContextType}"]` +
            (changedContextId ? `[data-notification-context-id="${changedContextId}"]` : ':not([data-notification-context-id])');

        document.querySelectorAll(selector).forEach(btn => updateMuteButtonState(btn, isMuted));
    }

    /**
     * Create a mute control button for use in templates
     * Returns HTML string for a mute button
     */
    function createMuteButton(contextType, contextId = '', classes = '') {
        const isMuted = typeof NotificationManager !== 'undefined' ? 
            NotificationManager.isMuted(contextType, contextId) : 
            false;

        const buttonClasses = `notification-mute-btn ${isMuted ? 'is-muted' : ''} ${classes}`;
        const icon = isMuted ? 'ri-notification-off-line' : 'ri-notification-2-line';
        const title = isMuted ? 'Click to unmute notifications' : 'Click to mute notifications';
        const dataContextId = contextId ? `data-notification-context-id="${contextId}"` : '';

        return `
            <button type="button" 
                    class="${buttonClasses}" 
                    data-notification-mute-btn="${contextType}"
                    ${dataContextId}
                    title="${title}"
                    aria-label="${title}"
                    aria-pressed="${isMuted}">
                <i class="${icon}"></i>
            </button>
        `;
    }

    /**
     * Create a mute control panel for a page
     * Returns HTML string for a control panel
     */
    function createControlPanel(pageContextType, showGlobalMute = true, showGroupMutes = []) {
        let html = '<div class="notification-control-panel">';
        html += '<label for="">Notification Mute Controls:</label>';

        if (showGlobalMute) {
            html += `
                <button type="button" 
                        class="btn btn-sm btn-outline-primary notification-mute-btn"
                        data-notification-mute-btn="${pageContextType}"
                        title="Mute all notifications on this page">
                    <i class="ri-notification-2-line"></i>
                    <span class="ms-1 d-none d-sm-inline">All</span>
                </button>
            `;
        }

        showGroupMutes.forEach(groupId => {
            html += `
                <button type="button" 
                        class="btn btn-sm btn-outline-secondary notification-mute-btn"
                        data-notification-mute-btn="${pageContextType}"
                        data-notification-context-id="${groupId}"
                        title="Mute notifications for this group">
                    <i class="ri-notification-2-line"></i>
                    <span class="ms-1 d-none d-sm-inline">Group ${groupId}</span>
                </button>
            `;
        });

        html += '</div>';
        return html;
    }

    /**
     * Create a mute dropdown menu for a card/element
     * Returns HTML string
     */
    function createMuteDropdown(contextType, contextId, showOptions = []) {
        const isMuted = typeof NotificationManager !== 'undefined' ? 
            NotificationManager.isMuted(contextType, contextId) : 
            false;

        let html = `
            <div class="dropdown">
                <button class="btn btn-sm btn-link dropdown-toggle p-0" 
                        type="button" 
                        data-bs-toggle="dropdown"
                        aria-expanded="false"
                        title="Notification options">
                    <i class="ri-notification-settings-line"></i>
                </button>
                <ul class="dropdown-menu dropdown-menu-end notification-settings-dropdown">
                    <li class="notification-settings-item">
                        <label for="notif-mute-${contextId}">
                            Mute notifications
                        </label>
                        <input type="checkbox" 
                               id="notif-mute-${contextId}"
                               class="form-check-input notification-mute-toggle"
                               data-notification-mute-btn="${contextType}"
                               data-notification-context-id="${contextId}"
                               ${isMuted ? 'checked' : ''}>
                    </li>
        `;

        if (showOptions.includes('clear')) {
            html += `
                    <li><hr class="dropdown-divider"></li>
                    <li class="dropdown-item">
                        <a href="javascript:void(0);" 
                           class="notification-clear-item"
                           data-notification-clear-context="${contextType}"
                           data-notification-clear-id="${contextId}">
                            <i class="ri-delete-bin-line me-2"></i> Clear notifications
                        </a>
                    </li>
            `;
        }

        html += `
                </ul>
            </div>
        `;

        return html;
    }

    /**
     * Auto-initialize all mute buttons and toggles on the page
     */
    function autoInitialize() {
        // Initialize all mute button states
        document.querySelectorAll('[data-notification-mute-btn]').forEach(btn => {
            const contextType = btn.dataset.notificationMuteBtn || '';
            const contextId = btn.dataset.notificationContextId || '';
            if (!contextType) return;
            const isMuted = NotificationManager?.isMuted(contextType, contextId) ?? false;
            updateMuteButtonState(btn, isMuted);
        });

        // Handle checkbox toggles for mute
        document.addEventListener('change', (e) => {
            if (e.target.classList.contains('notification-mute-toggle')) {
                const contextType = e.target.dataset.notificationMuteBtn;
                const contextId = e.target.dataset.notificationContextId || '';

                if (e.target.checked) {
                    NotificationManager?.mute(contextType, contextId);
                } else {
                    NotificationManager?.unmute(contextType, contextId);
                }
            }
        });

        // Handle mute button clicks globally (for sidebar and shared controls)
        document.addEventListener('click', (e) => {
            const muteBtn = e.target.closest('[data-notification-mute-btn]');
            if (muteBtn) {
                e.preventDefault();
                handleMuteButtonClick(muteBtn, '');
                return;
            }

            const clearLink = e.target.closest('.notification-clear-item');
            if (clearLink) {
                e.preventDefault();
                const contextType = clearLink.dataset.notificationClearContext;
                const contextId = clearLink.dataset.notificationClearId;
                NotificationManager?.clearNotifications(contextType, contextId);
            }
        });

        // Keep all controls synchronized across pages/tabs
        document.addEventListener('notificationPreferenceChanged', (e) => {
            const { contextType, contextId, isMuted } = e.detail || {};
            updateAllControlsForContext(contextType, contextId, isMuted);
        });
    }

    // Auto-initialize on document ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', autoInitialize);
    } else {
        autoInitialize();
    }

    // Public API
    return {
        initPageControls,
        createMuteButton,
        createControlPanel,
        createMuteDropdown,
        updateMuteButtonState
    };
})();
