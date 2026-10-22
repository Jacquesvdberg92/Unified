(function () {
    'use strict';

    const isAuthenticated = document.querySelector('body')?.classList.contains('authentication-background') !== true
        && !!document.querySelector('#messageDropdown');

    if (!isAuthenticated) {
        return;
    }

    const app = document.getElementById('csMessagingApp');
    const currentUserId = app?.dataset.currentUserId || null;
    const activeConversationId = app ? (parseInt(app.dataset.activeConversationId || '0', 10) || 0) : 0;
    const activePath = (window.location.pathname || '').toLowerCase();
    const onMessagingPage = activePath.startsWith('/csmessaging');

    const hasSignalR = typeof window.signalR !== 'undefined' && window.signalR?.HubConnectionBuilder;
    if (!hasSignalR) {
        console.warn('[LiveNotifications] SignalR client unavailable.');
        return;
    }

    const connection = new window.signalR.HubConnectionBuilder()
        .withUrl('/hubs/cs-messaging')
        .withAutomaticReconnect()
        .build();

    let hubStarted = false;

    window.UnifiedLiveNotifications = {
        connection,
        isConnected: function () {
            return hubStarted && String(connection.state || '').toLowerCase() === 'connected';
        }
    };

    function getPreviewText(message) {
        const body = (message?.body || '').trim();
        if (body) {
            return body;
        }

        if (message?.gifUrl) {
            return 'GIF';
        }

        return 'Message';
    }

    function notifyIncomingMessage(evt) {
        if (typeof NotificationManager === 'undefined' || !evt?.message) {
            return;
        }

        const message = evt.message;
        if (currentUserId && message.authorUserId === currentUserId) {
            return;
        }

        if (onMessagingPage && activeConversationId && evt.conversationId === activeConversationId) {
            return;
        }

        const previewText = getPreviewText(message);
        const authorName = message?.authorUser?.displayName || 'Someone';

        NotificationManager.handleNotification({
            type: 'newMessage',
            contextType: 'CsMessaging',
            contextId: String(evt.conversationId || ''),
            title: `New message from ${authorName}`,
            message: previewText.length > 60 ? `${previewText.slice(0, 60)}…` : previewText,
            sound: true,
            visual: true,
            toast: true
        });
    }

    connection.on('MessageAdded', function (evt) {
        notifyIncomingMessage(evt);
    });

    connection.on('ConversationChanged', function () {
        if (!onMessagingPage && typeof NotificationManager !== 'undefined') {
            NotificationManager.updateNotificationBell();
        }
    });

    connection.on('WorkDistributionMentionNotification', function (evt) {
        if (typeof NotificationManager === 'undefined') {
            return;
        }

        const contextId = String(evt?.date || evt?.contextId || '');
        const author = evt?.author || 'Someone';
        const dateLabel = evt?.date || contextId;

        NotificationManager.handleNotification({
            type: 'mention',
            contextType: 'WorkDistribution',
            contextId: contextId,
            title: 'You were mentioned',
            message: `${author} mentioned you in work distribution (${dateLabel})`,
            sound: true,
            visual: true,
            toast: true
        });
    });

    connection.on('UpdatePosted', function (evt) {
        if (!evt?.id) {
            return;
        }

        window.UnifiedUpdatesUnread?.add(evt.id);

        if (typeof NotificationManager === 'undefined') {
            return;
        }

        NotificationManager.handleNotification({
            type: 'update',
            contextType: 'Updates',
            title: evt.isPinned ? 'New pinned update' : 'New update posted',
            message: `${evt.title || 'Untitled'} (by ${evt.authorName || 'Someone'})`,
            url: `/Updates/Detail/${evt.id}`,
            sound: true,
            visual: true,
            toast: true
        });
    });

    connection.on('WithdrawalNotification', function (evt) {
        if (typeof NotificationManager === 'undefined') {
            return;
        }

        NotificationManager.handleNotification({
            type: 'notification',
            contextType: 'WdList',
            contextId: String(evt?.id || ''),
            title: evt?.title || 'WD List',
            message: evt?.message || 'A withdrawal was updated',
            url: evt?.url || undefined,
            sound: true,
            visual: true,
            toast: true
        });
    });

    connection.onreconnected(function () {
        if (!onMessagingPage && typeof NotificationManager !== 'undefined') {
            NotificationManager.updateNotificationBell();
        }
    });

    connection.start()
        .then(function () {
            hubStarted = true;
        })
        .catch(function (error) {
            console.warn('[LiveNotifications] Failed to connect:', error);
        });
})();
