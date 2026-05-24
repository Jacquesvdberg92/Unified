(function () {
    'use strict';

    const app = document.getElementById('csMessagingApp');
    if (!app) return;

    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const antiForgery = tokenEl ? tokenEl.value : '';

    const conversationListEl = document.getElementById('conversationList');
    const groupConversationListEl = document.getElementById('groupConversationList');
    const contactsListEl = document.getElementById('contactsList');
    const recentCountBadgeEl = document.getElementById('recentCountBadge');
    const groupsCountBadgeEl = document.getElementById('groupsCountBadge');
    const contactsCountBadgeEl = document.getElementById('contactsCountBadge');

    const messageThreadEl = document.getElementById('messageThread');
    const chatTitleEl = document.getElementById('chatTitle');
    const chatMembersEl = document.getElementById('chatMembers');
    const messageBodyEl = document.getElementById('messageBody');
    const conversationSearchEl = document.getElementById('conversationSearch');
    const openConversationPaneBtn = document.getElementById('openConversationPane');
    const closeConversationPaneBtn = document.getElementById('closeConversationPane');
    const chatSidebarEl = document.getElementById('chatSidebar');

    const startDirectUserEl = document.getElementById('startDirectUser');
    const startDirectBtn = document.getElementById('startDirectBtn');
    const groupNameEl = document.getElementById('groupName');
    const createGroupBtn = document.getElementById('createGroupBtn');
    const sendMessageBtn = document.getElementById('sendMessageBtn');

    const openAttachmentBtn = document.getElementById('openAttachmentBtn');
    const attachmentInputEl = document.getElementById('attachmentInput');
    const openEmojiPickerBtn = document.getElementById('openEmojiPickerBtn');
    const openGifPickerBtn = document.getElementById('openGifPickerBtn');
    const closeEmojiPickerBtn = document.getElementById('closeEmojiPickerBtn');
    const emojiPickerPopoverEl = document.getElementById('emojiPickerPopover');
    const emojiPickerBodyEl = document.getElementById('emojiPickerBody');
    const emojiSearchInputEl = document.getElementById('emojiSearchInput');
    const mentionSuggestionsEl = document.getElementById('mentionSuggestions');
    const gifSearchInputEl = document.getElementById('gifSearchInput');
    const gifSearchBtn = document.getElementById('gifSearchBtn');
    const gifSearchResultsEl = document.getElementById('gifSearchResults');

    const pastedPreviewWrapEl = document.getElementById('pastedPreviewWrap');
    const pastedPreviewImgEl = document.getElementById('pastedPreviewImg');
    const clearPastedPreviewBtn = document.getElementById('clearPastedPreviewBtn');

    const urls = {
        conversations: app.dataset.conversationsUrl,
        conversationBase: app.dataset.conversationUrlBase,
        startDirect: app.dataset.startDirectUrl,
        createGroup: app.dataset.createGroupUrl,
        addMessageBase: app.dataset.addMessageUrlBase,
        toggleReactionBase: app.dataset.toggleReactionUrlBase,
        markRead: app.dataset.markReadUrl,
        gifSearch: app.dataset.gifSearchUrl,
        uploadPaste: app.dataset.uploadPasteUrl,
        emojiList: app.dataset.emojiListUrl,
        editMessageBase: app.dataset.editMessageUrlBase,
        deleteMessageBase: app.dataset.deleteMessageUrlBase,
        addMemberBase: app.dataset.addMemberUrlBase,
        removeMemberBase: app.dataset.removeMemberUrlBase
    };

    let activeConversationId = parseInt(app.dataset.activeConversationId || '0', 10) || 0;
    const currentUserId = app.dataset.currentUserId || '';
    let activeConversationMembers = [];
    let activeConversationCanManage = false;
    let reactionPickerTargetMessageId = 0;
    let pastedImageUrl = '';
    let sendingMessage = false;
    let mentionCandidates = [];
    let mentionState = null;
    let emojiPickMode = 'compose';
    let activeConversationCache = new Map();
    let markReadTimerId = 0;
    let emojiSet = ['😀', '😁', '😂', '🤣', '😊', '😍', '😘', '😎', '🤔', '😢', '😡', '👍', '👏', '🙌', '🔥', '❤️', '💯', '🎉', '🙏', '👀', '🤝', '✅', '❌', '👑'];
    let emojiCatalog = emojiSet.map(e => ({ emoji: e, search: e.toLowerCase() }));
    const maxEmojiCount = 800;
    let emojiSearchText = '';

    const gifModal = window.bootstrap ? new bootstrap.Modal(document.getElementById('gifPickerModal')) : null;

    function toast(msg, ok) {
        if (typeof showToast === 'function') {
            showToast(msg, ok);
        } else {
            console[ok ? 'log' : 'warn'](msg);
        }
    }

    function withId(url, id) {
        return url.endsWith('/0') ? (url.slice(0, -1) + String(id)) : (url + '/' + id);
    }

    function csrfHeaders() {
        return antiForgery
            ? { 'RequestVerificationToken': antiForgery }
            : {};
    }

    async function getJson(url) {
        const res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        return await res.json();
    }

    async function postJson(url, body) {
        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                ...csrfHeaders()
            },
            body: JSON.stringify(body || {})
        });
        return await res.json();
    }

    function buildConversationItem(c) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'conversation-item' + (c.conversationId === activeConversationId ? ' active' : '');
        btn.dataset.conversationId = c.conversationId;
        btn.dataset.searchText = `${(c.displayName || '').toLowerCase()} ${(c.lastMessagePreview || '').toLowerCase()}`;

        const unread = c.unreadCount > 0 ? `<span class="conversation-unread">${c.unreadCount}</span>` : '';
        const time = c.lastMessageAt ? new Date(c.lastMessageAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';
        const avatar = (c.displayName || '?').trim().charAt(0).toUpperCase();

        btn.innerHTML =
            `<span class="conversation-avatar">${escapeHtml(avatar)}</span>` +
            `<span class="conversation-body">` +
                `<span class="conversation-title-row">` +
                    `<span class="conversation-title">${escapeHtml(c.displayName || '')}</span>` +
                    `<span class="conversation-time">${escapeHtml(time)}</span>` +
                `</span>` +
                `<span class="conversation-preview text-truncate">${escapeHtml(c.lastMessagePreview || '')}</span>` +
            `</span>` +
            unread;

        return btn;
    }

    function cacheConversations(conversations) {
        activeConversationCache = new Map();
        (conversations || []).forEach(c => {
            if (c && c.conversationId) {
                activeConversationCache.set(c.conversationId, { ...c });
            }
        });
    }

    function getCachedConversations() {
        return Array.from(activeConversationCache.values())
            .sort((a, b) => {
                const aTime = a.lastMessageAt ? Date.parse(a.lastMessageAt) : 0;
                const bTime = b.lastMessageAt ? Date.parse(b.lastMessageAt) : 0;
                return bTime - aTime;
            });
    }

    function updateConversationListItemSnapshot(conversationId, patch) {
        if (!conversationId || !activeConversationCache.has(conversationId)) return;

        const current = activeConversationCache.get(conversationId);
        activeConversationCache.set(conversationId, {
            ...current,
            ...patch
        });

        renderConversations(getCachedConversations());
    }

    function renderConversations(conversations) {
        cacheConversations(conversations);

        if (conversationListEl) conversationListEl.innerHTML = '';
        if (groupConversationListEl) groupConversationListEl.innerHTML = '';

        conversations.forEach(c => {
            if (conversationListEl) {
                conversationListEl.appendChild(buildConversationItem(c));
            }

            if (c.isGroup && groupConversationListEl) {
                groupConversationListEl.appendChild(buildConversationItem(c));
            }
        });

        const recentUnread = conversations.reduce((sum, c) => sum + (c.unreadCount || 0), 0);
        const groupUnread = conversations
            .filter(c => c.isGroup)
            .reduce((sum, c) => sum + (c.unreadCount || 0), 0);

        if (recentCountBadgeEl) recentCountBadgeEl.textContent = String(recentUnread);
        if (groupsCountBadgeEl) groupsCountBadgeEl.textContent = String(groupUnread);
        if (contactsCountBadgeEl && contactsListEl) {
            const contactCount = contactsListEl.querySelectorAll('.contact-item').length;
            contactsCountBadgeEl.textContent = String(contactCount);
        }

        applyConversationSearch();
    }

    function renderMessages(detail) {
        chatTitleEl.textContent = detail.displayName || 'Conversation';

        activeConversationMembers = (detail.members || []).map(m => ({
            userId: m.userId || '',
            displayName: m.displayName || ''
        }));
        activeConversationCanManage = !!detail.canManageMembers;

        chatMembersEl.textContent = activeConversationMembers.map(m => m.displayName).join(', ');

        const mentionByLabel = new Map();
        mentionByLabel.set('all', { label: 'all', value: '@all' });

        activeConversationMembers
            .filter(m => m.userId !== currentUserId)
            .forEach(m => {
                const label = (m.displayName || '').trim();
                if (!label) return;
                mentionByLabel.set(label.toLowerCase(), {
                    userId: m.userId,
                    label,
                    value: `@${label}`
                });
            });

        mentionCandidates = Array.from(mentionByLabel.values())
            .sort((a, b) => a.label.localeCompare(b.label));
        mentionState = null;
        hideMentionSuggestions();

        messageThreadEl.innerHTML = '';
        (detail.messages || []).forEach(m => {
            messageThreadEl.appendChild(renderMessageCard(m));
        });

        messageThreadEl.scrollTop = messageThreadEl.scrollHeight;
    }

    function renderMessageCard(m) {
        const row = document.createElement('div');
        const mine = (m.authorUserId || '') === currentUserId;
        row.className = 'cs-msg-row ' + (mine ? 'mine' : 'theirs');
        row.dataset.messageId = m.messageId;

        const dt = m.createdAt ? new Date(m.createdAt) : null;
        const dateText = dt ? dt.toLocaleString() : '';

        let bodyHtml = '<div class="text-muted fst-italic">Message deleted</div>';
        if (!m.isDeleted) {
            bodyHtml = `<div class="cs-msg-body">${escapeHtml(m.body || '')}</div>`;
            if (m.gifUrl) {
                bodyHtml += `<div class="cs-msg-gif mt-1"><img src="${escapeAttribute(m.gifUrl)}" alt="GIF" class="img-fluid rounded border" /></div>`;
            }
        }

        const reactions = (m.reactions || []).map(r => {
            const cls = r.reactedByCurrentUser ? 'reaction-chip active' : 'reaction-chip';
            return `<button type="button" class="${cls}" data-message-id="${m.messageId}" data-emoji="${escapeAttribute(r.emoji)}">${escapeHtml(r.emoji)} <span>${r.count}</span></button>`;
        }).join('');

        const statusHtml = mine
            ? `<span class="cs-msg-status" title="${m.isReadByAllOthers ? 'Read' : 'Sent'}">${m.isReadByAllOthers ? '✓✓' : '✓'}</span>`
            : '';

        const actionsHtml = (mine && !m.isDeleted)
            ? `<span class="cs-msg-actions ms-1">` +
              `<button type="button" class="btn-msg-edit" data-message-id="${m.messageId}" title="Edit"><i class="bx bx-edit-alt"></i></button>` +
              `<button type="button" class="btn-msg-delete" data-message-id="${m.messageId}" title="Delete"><i class="bx bx-trash"></i></button>` +
              `</span>`
            : '';

        row.innerHTML =
            `<div class="cs-msg-bubble">` +
                `<div class="cs-msg-meta">` +
                    `<span class="cs-msg-author">${escapeHtml(m.authorName || 'User')}</span>` +
                    `<span class="cs-msg-time">${escapeHtml(dateText)}</span>` +
                    statusHtml +
                    actionsHtml +
                `</div>` +
                bodyHtml +
                `<div class="reaction-container mt-2">` +
                    reactions +
                    `<button type="button" class="quick-reaction" data-message-id="${m.messageId}" data-emoji="+" title="Add reaction">+</button>` +
                `</div>` +
            `</div>`;

        return row;
    }

    function getMessageRow(messageId) {
        if (!messageId) return null;
        return messageThreadEl.querySelector(`.cs-msg-row[data-message-id="${messageId}"]`);
    }

    function setMessageStatus(row, isRead) {
        if (!row || !row.classList.contains('mine')) return;

        const statusEl = row.querySelector('.cs-msg-status');
        if (!statusEl) return;

        statusEl.title = isRead ? 'Read' : 'Sent';
        statusEl.textContent = isRead ? '✓✓' : '✓';
    }

    function applyReactionUpdate(evt) {
        if (!evt || !evt.messageId || !evt.emoji) return;

        const row = getMessageRow(evt.messageId);
        if (!row) return;

        const container = row.querySelector('.reaction-container');
        if (!container) return;

        const selector = `.reaction-chip[data-message-id="${evt.messageId}"][data-emoji="${CSS.escape(evt.emoji)}"]`;
        const chip = container.querySelector(selector);

        if ((evt.count || 0) <= 0) {
            if (chip) chip.remove();
            return;
        }

        const reactedByCurrentUser = evt.actorUserId === currentUserId
            ? !!evt.reactedByCurrentUser
            : !!chip?.classList.contains('active');

        if (chip) {
            chip.classList.toggle('active', reactedByCurrentUser);
            const countEl = chip.querySelector('span');
            if (countEl) countEl.textContent = String(evt.count);
            return;
        }

        const insertBefore = container.querySelector('.quick-reaction');
        const newChip = document.createElement('button');
        newChip.type = 'button';
        newChip.className = `reaction-chip${reactedByCurrentUser ? ' active' : ''}`;
        newChip.dataset.messageId = String(evt.messageId);
        newChip.dataset.emoji = evt.emoji;
        newChip.innerHTML = `${escapeHtml(evt.emoji)} <span>${evt.count}</span>`;

        if (insertBefore) {
            container.insertBefore(newChip, insertBefore);
        } else {
            container.appendChild(newChip);
        }
    }

    function markMineAsReadForDirectConversation(readerUserId) {
        if (!readerUserId || readerUserId === currentUserId) return;
        if (activeConversationMembers.length !== 2) return;

        messageThreadEl.querySelectorAll('.cs-msg-row.mine').forEach(row => {
            setMessageStatus(row, true);
        });
    }

    function scheduleMarkRead(conversationId, delayMs) {
        if (!conversationId) return;

        if (markReadTimerId) {
            window.clearTimeout(markReadTimerId);
        }

        markReadTimerId = window.setTimeout(async function () {
            markReadTimerId = 0;
            await postJson(urls.markRead, { conversationId });
        }, Math.max(0, delayMs || 0));
    }

    async function refreshConversations() {
        const data = await getJson(urls.conversations);
        if (!data.success) return;
        renderConversations(data.conversations || []);
    }

    async function safeJoinConversation(id) {
        if (!connection || !id) return;

        const state = String(connection.state || '').toLowerCase();
        if (state !== 'connected') return;

        try {
            await connection.invoke('JoinConversation', id);
        } catch {
            // No-op, conversation remains usable without immediate group join.
        }
    }

    async function loadConversation(id) {
        if (!id) return;
        const data = await getJson(withId(urls.conversationBase, id));
        if (!data.success || !data.detail) return;

        activeConversationId = id;
        app.dataset.activeConversationId = String(id);
        document.querySelectorAll('.conversation-item').forEach(el => {
            el.classList.toggle('active', parseInt(el.dataset.conversationId || '0', 10) === id);
        });

        renderMessages(data.detail);
        updateConversationListItemSnapshot(id, { unreadCount: 0 });
        scheduleMarkRead(id, 0);
        await safeJoinConversation(id);

        hideMentionSuggestions();

        if (chatSidebarEl) {
            chatSidebarEl.classList.remove('show-mobile');
        }
    }

    function clearPastedPreview() {
        pastedImageUrl = '';
        if (pastedPreviewImgEl) pastedPreviewImgEl.removeAttribute('src');
        if (pastedPreviewWrapEl) pastedPreviewWrapEl.classList.add('d-none');
    }

    async function sendMessage(overrideGifUrl) {
        if (sendingMessage) return false;

        if (!activeConversationId) {
            toast('Select a conversation first.', false);
            return false;
        }

        const composedBody = messageBodyEl.value || '';
        const gifToSend = overrideGifUrl || pastedImageUrl || null;
        const payload = {
            body: composedBody,
            gifUrl: gifToSend
        };

        sendingMessage = true;
        try {
            const data = await postJson(withId(urls.addMessageBase, activeConversationId), payload);
            if (!data.success) {
                toast(data.error || 'Failed to send message.', false);
                return false;
            }

            if (data.message) {
                const preview = (data.message.body || '').trim();
                const previewText = preview || (data.message.gifUrl ? 'GIF' : 'Message');
                updateConversationListItemSnapshot(activeConversationId, {
                    unreadCount: 0,
                    lastMessagePreview: previewText.length > 80 ? `${previewText.slice(0, 80)}…` : previewText,
                    lastMessageAt: data.message.createdAt || new Date().toISOString()
                });
            }

            messageBodyEl.value = '';
            clearPastedPreview();
            return true;
        } finally {
            sendingMessage = false;
        }
    }

    async function startDirect(explicitUserId) {
        const userId = explicitUserId || startDirectUserEl.value;
        if (!userId) return;

        const form = new URLSearchParams();
        form.append('userId', userId);

        const res = await fetch(urls.startDirect, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                'X-Requested-With': 'XMLHttpRequest',
                ...csrfHeaders()
            },
            body: form.toString()
        });
        const data = await res.json();

        if (!data.success) {
            toast(data.error || 'Failed to start direct conversation.', false);
            return;
        }

        if (startDirectUserEl) startDirectUserEl.value = '';
        await refreshConversations();
        await loadConversation(data.conversationId);
    }

    async function createGroup() {
        const groupName = (groupNameEl.value || '').trim();
        if (!groupName) {
            toast('Group name is required.', false);
            return;
        }

        const memberCheckboxes = document.querySelectorAll('.group-member-checkbox:checked');
        const members = Array.from(memberCheckboxes).map(cb => cb.value);

        if (!members.length) {
            toast('Please select at least one member.', false);
            return;
        }

        const data = await postJson(urls.createGroup, {
            name: groupName,
            memberUserIds: members
        });

        if (!data.success) {
            toast(data.error || 'Failed to create group.', false);
            return;
        }

        groupNameEl.value = '';
        document.querySelectorAll('.group-member-checkbox').forEach(cb => cb.checked = false);

        await refreshConversations();
        await loadConversation(data.conversationId);
        toast('Group created successfully.', true);
    }

    async function toggleReaction(messageId, emoji) {
        const data = await postJson(withId(urls.toggleReactionBase, messageId), { emoji });
        if (!data.success) {
            toast(data.error || 'Failed to update reaction.', false);
            return;
        }
    }

    async function editMessage(messageId, newBody) {
        const data = await postJson(withId(urls.editMessageBase, messageId), { body: newBody });
        if (!data.success) {
            toast(data.error || 'Failed to edit message.', false);
            return;
        }
    }

    async function deleteMessage(messageId) {
        const data = await postJson(withId(urls.deleteMessageBase, messageId), {});
        if (!data.success) {
            toast(data.error || 'Failed to delete message.', false);
        }
    }

    async function addMember(conversationId, userId) {
        const data = await postJson(withId(urls.addMemberBase, conversationId), { userId });
        if (!data.success) {
            toast(data.error || 'Failed to add member.', false);
            return null;
        }
        return data.member;
    }

    async function removeMember(conversationId, userId) {
        const form = new URLSearchParams();
        form.append('userId', userId);
        const res = await fetch(withId(urls.removeMemberBase, conversationId) + '?' + form.toString(), {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                'X-Requested-With': 'XMLHttpRequest',
                ...csrfHeaders()
            }
        });
        const data = await res.json();
        if (!data.success) {
            toast(data.error || 'Failed to remove member.', false);
        }
        return data.success;
    }

    function applyMessageEdit(message) {
        const row = getMessageRow(message.messageId);
        if (!row) return;

        const bodyEl = row.querySelector('.cs-msg-body');
        if (bodyEl) {
            bodyEl.textContent = message.body || '';
        }

        const metaEl = row.querySelector('.cs-msg-meta');
        if (metaEl && message.isEdited) {
            let editedTag = metaEl.querySelector('.cs-msg-edited');
            if (!editedTag) {
                editedTag = document.createElement('span');
                editedTag.className = 'cs-msg-edited text-muted ms-1';
                editedTag.style.fontSize = '.7rem';
                editedTag.textContent = '(edited)';
                const timeEl = metaEl.querySelector('.cs-msg-time');
                if (timeEl) {
                    timeEl.after(editedTag);
                } else {
                    metaEl.appendChild(editedTag);
                }
            }
        }
    }

    function applyMessageDelete(messageId) {
        const row = getMessageRow(messageId);
        if (!row) return;

        const bubble = row.querySelector('.cs-msg-bubble');
        if (!bubble) return;

        const bodyEl = bubble.querySelector('.cs-msg-body');
        if (bodyEl) {
            const deleted = document.createElement('div');
            deleted.className = 'text-muted fst-italic';
            deleted.textContent = 'Message deleted';
            bodyEl.replaceWith(deleted);
        }

        const gifEl = bubble.querySelector('.cs-msg-gif');
        if (gifEl) gifEl.remove();

        const actionsEl = row.querySelector('.cs-msg-actions');
        if (actionsEl) actionsEl.remove();
    }

    function renderGroupMemberList(members, canManage) {
        const listEl = document.getElementById('groupMemberList');
        if (!listEl) return;

        listEl.innerHTML = members.map(m => {
            const removeBtn = canManage
                ? `<button type="button" class="btn btn-sm btn-outline-danger btn-remove-member ms-2" data-user-id="${escapeAttribute(m.userId)}"><i class="bx bx-minus"></i></button>`
                : '';
            return `<li class="d-flex align-items-center justify-content-between mb-1" data-member-user-id="${escapeAttribute(m.userId)}">` +
                `<span>${escapeHtml(m.displayName || m.userId)}</span>${removeBtn}</li>`;
        }).join('');
    }

    async function searchGifs() {
        const term = (gifSearchInputEl?.value || '').trim();
        if (term.length < 2) {
            gifSearchResultsEl.innerHTML = '<div class="col-12 text-muted small">Type at least 2 characters.</div>';
            return;
        }

        gifSearchResultsEl.innerHTML = '<div class="col-12 text-muted small">Searching...</div>';

        try {
            const data = await getJson(`${urls.gifSearch}?q=${encodeURIComponent(term)}`);
            if (!data.success) {
                gifSearchResultsEl.innerHTML = `<div class="col-12 text-danger small">${escapeHtml(data.error || 'GIF search failed.')}</div>`;
                return;
            }

            const gifs = data.gifs || [];
            if (!gifs.length) {
                gifSearchResultsEl.innerHTML = '<div class="col-12 text-muted small">No GIFs found.</div>';
                return;
            }

            gifSearchResultsEl.innerHTML = gifs.map(g =>
                `<div class="col-6 col-md-4 col-lg-3">` +
                    `<button type="button" class="btn p-0 border w-100 gif-pick" data-url="${escapeAttribute(g.previewUrl || '')}">` +
                        `<img src="${escapeAttribute(g.previewUrl || '')}" class="img-fluid rounded" alt="GIF" />` +
                    `</button>` +
                `</div>`
            ).join('');
        } catch {
            gifSearchResultsEl.innerHTML = '<div class="col-12 text-danger small">GIF search failed.</div>';
        }
    }

    async function uploadPastedFile(file) {
        const form = new FormData();
        form.append('image', file, file.name || 'pasted-image.png');

        const res = await fetch(urls.uploadPaste, {
            method: 'POST',
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                ...csrfHeaders()
            },
            body: form
        });

        const data = await res.json();
        if (!data.success || !data.imageUrl) {
            toast(data.error || 'Failed to upload pasted image.', false);
            return;
        }

        pastedImageUrl = data.imageUrl;
        pastedPreviewImgEl.src = pastedImageUrl;
        pastedPreviewWrapEl.classList.remove('d-none');
    }

    async function uploadAttachmentFile(file) {
        if (!file) return;

        const maxSize = 5 * 1024 * 1024;
        if (file.size > maxSize) {
            toast('Image is too large (max 5 MB).', false);
            return;
        }

        await uploadPastedFile(file);
        messageBodyEl.focus();
    }

    function positionEmojiPicker(anchorEl) {
        if (!emojiPickerPopoverEl) return;

        const margin = 8;
        const viewportWidth = window.innerWidth;
        const viewportHeight = window.innerHeight;

        const anchorRect = anchorEl?.getBoundingClientRect();
        const pickerRect = emojiPickerPopoverEl.getBoundingClientRect();

        let left = margin;
        let top = margin;

        if (anchorRect) {
            left = anchorRect.left;
            top = anchorRect.bottom + margin;

            const overflowRight = left + pickerRect.width + margin - viewportWidth;
            if (overflowRight > 0) {
                left = Math.max(margin, left - overflowRight);
            }

            const overflowBottom = top + pickerRect.height + margin - viewportHeight;
            if (overflowBottom > 0) {
                const above = anchorRect.top - pickerRect.height - margin;
                top = above >= margin ? above : Math.max(margin, viewportHeight - pickerRect.height - margin);
            }
        }

        emojiPickerPopoverEl.style.left = `${Math.round(left)}px`;
        emojiPickerPopoverEl.style.top = `${Math.round(top)}px`;
    }

    function openEmojiPicker(mode, targetMessageId, anchorEl) {
        if (!emojiPickerPopoverEl || !emojiPickerBodyEl) return;

        emojiPickMode = mode || 'compose';
        reactionPickerTargetMessageId = targetMessageId || 0;
        emojiSearchText = '';

        if (emojiSearchInputEl) {
            emojiSearchInputEl.value = '';
        }

        renderEmojiPicker();
        emojiPickerPopoverEl.classList.remove('d-none');
        positionEmojiPicker(anchorEl || openEmojiPickerBtn || messageBodyEl);

        if (emojiSearchInputEl) {
            emojiSearchInputEl.focus();
        }
    }

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function escapeAttribute(s) {
        return escapeHtml(s).replace(/'/g, '&#39;');
    }

    function applyConversationSearch() {
        if (!conversationSearchEl) return;

        const q = (conversationSearchEl.value || '').trim().toLowerCase();

        [conversationListEl, groupConversationListEl].forEach(listEl => {
            if (!listEl) return;
            const items = listEl.querySelectorAll('.conversation-item');
            items.forEach(item => {
                const hay = item.dataset.searchText || '';
                item.style.display = !q || hay.includes(q) ? '' : 'none';
            });
        });

        if (contactsListEl) {
            const contactItems = contactsListEl.querySelectorAll('.contact-item');
            contactItems.forEach(item => {
                const hay = item.dataset.searchText || '';
                const hostLi = item.closest('li');
                if (hostLi) {
                    hostLi.style.display = !q || hay.includes(q) ? '' : 'none';
                }
            });
        }
    }

    function hideMentionSuggestions() {
        mentionState = null;
        if (!mentionSuggestionsEl) return;
        mentionSuggestionsEl.classList.add('d-none');
        mentionSuggestionsEl.innerHTML = '';
    }

    function insertMentionValue(value) {
        if (!value || !messageBodyEl) return;

        const text = messageBodyEl.value || '';
        const cursor = messageBodyEl.selectionStart ?? text.length;
        const state = mentionState;
        if (!state || typeof state.start !== 'number') {
            messageBodyEl.value += `${value} `;
            messageBodyEl.focus();
            hideMentionSuggestions();
            return;
        }

        const before = text.slice(0, state.start);
        const after = text.slice(cursor);
        const insertion = `${value} `;
        const nextText = `${before}${insertion}${after}`;

        messageBodyEl.value = nextText;
        const caret = before.length + insertion.length;
        messageBodyEl.setSelectionRange(caret, caret);
        messageBodyEl.focus();
        hideMentionSuggestions();
    }

    function renderMentionSuggestions(matches) {
        if (!mentionSuggestionsEl) return;

        if (!matches.length) {
            hideMentionSuggestions();
            return;
        }

        mentionSuggestionsEl.innerHTML = matches.map((item, index) =>
            `<button type="button" class="mention-option${index === 0 ? ' active' : ''}" data-value="${escapeAttribute(item.value)}" data-index="${index}">` +
                `<span class="fw-semibold">${escapeHtml(item.value)}</span>` +
            `</button>`
        ).join('');

        mentionSuggestionsEl.classList.remove('d-none');
        mentionState = {
            ...mentionState,
            matches,
            selectedIndex: 0
        };
    }

    function updateMentionSuggestions() {
        if (!messageBodyEl || !mentionCandidates.length) {
            hideMentionSuggestions();
            return;
        }

        const text = messageBodyEl.value || '';
        const cursor = messageBodyEl.selectionStart ?? text.length;
        const uptoCursor = text.slice(0, cursor);
        const atIndex = uptoCursor.lastIndexOf('@');

        if (atIndex < 0) {
            hideMentionSuggestions();
            return;
        }

        const charBefore = atIndex === 0 ? ' ' : uptoCursor[atIndex - 1];
        if (charBefore && !/\s/.test(charBefore)) {
            hideMentionSuggestions();
            return;
        }

        const rawQuery = uptoCursor.slice(atIndex + 1);
        if (rawQuery.includes(' ') || rawQuery.includes('\n') || rawQuery.includes('\t')) {
            hideMentionSuggestions();
            return;
        }

        const query = rawQuery.trim().toLowerCase();
        const matches = mentionCandidates.filter(item =>
            !query || item.label.toLowerCase().includes(query)
        ).slice(0, 8);

        mentionState = {
            start: atIndex,
            cursor,
            query,
            matches,
            selectedIndex: 0
        };

        renderMentionSuggestions(matches);
    }

    function moveMentionSelection(direction) {
        if (!mentionState?.matches?.length || !mentionSuggestionsEl) return;

        const maxIndex = mentionState.matches.length - 1;
        const next = Math.min(maxIndex, Math.max(0, mentionState.selectedIndex + direction));
        mentionState.selectedIndex = next;

        mentionSuggestionsEl.querySelectorAll('.mention-option').forEach(el => {
            const idx = parseInt(el.dataset.index || '-1', 10);
            el.classList.toggle('active', idx === next);
        });
    }

    function normalizeEmojiData(raw) {
        if (!raw || typeof raw !== 'object') return [];

        const values = [];
        Object.keys(raw).forEach(groupName => {
            const group = raw[groupName];
            if (!Array.isArray(group)) return;

            group.forEach(item => {
                const emoji = item?.emoji;
                if (!emoji) return;

                const keywords = Array.isArray(item.keywords) ? item.keywords.join(' ') : '';
                const description = item.description || '';

                values.push({
                    emoji,
                    search: `${emoji} ${description} ${keywords}`.toLowerCase()
                });
            });
        });

        const seen = new Set();
        const unique = [];
        values.forEach(v => {
            if (!v.emoji || seen.has(v.emoji)) return;
            seen.add(v.emoji);
            unique.push(v);
        });

        return unique;
    }

    async function loadEmojiSetAsync() {
        const listUrl = urls.emojiList;
        if (!listUrl) return;

        try {
            const res = await fetch(listUrl, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!res.ok) return;
            const raw = await res.json();
            const parsed = normalizeEmojiData(raw);
            if (parsed.length) {
                emojiCatalog = parsed.slice(0, maxEmojiCount);
                emojiSet = emojiCatalog.map(x => x.emoji);
            }
        } catch {
            // Keep fallback set
        }
    }

    function renderEmojiPicker() {
        if (!emojiPickerBodyEl) return;

        const q = (emojiSearchText || '').trim().toLowerCase();
        const filteredCatalog = !q
            ? emojiCatalog
            : emojiCatalog.filter(item => item.search.includes(q));
        const filtered = filteredCatalog.map(item => item.emoji);

        if (!filtered.length) {
            emojiPickerBodyEl.innerHTML = '<div class="small text-muted px-1 py-2">No emoji found.</div>';
            return;
        }

        emojiPickerBodyEl.innerHTML =
            `<div class="cs-emoji-grid">` +
            filtered.slice(0, maxEmojiCount).map(e => `<button type="button" class="btn btn-light border btn-sm emoji-pick" data-emoji="${escapeAttribute(e)}">${escapeHtml(e)}</button>`).join('') +
            `</div>`;
    }

    const hasSignalR = typeof window.signalR !== 'undefined' && window.signalR?.HubConnectionBuilder;
    const connection = hasSignalR
        ? new window.signalR.HubConnectionBuilder()
            .withUrl('/hubs/cs-messaging')
            .withAutomaticReconnect()
            .build()
        : null;

    if (connection) {
        connection.on('MessageAdded', async function (evt) {
            if (!evt) return;

            console.log(`[SignalR] MessageAdded event received:`, evt);

            const isIncomingForActive = evt.conversationId === activeConversationId && evt.message?.authorUserId !== currentUserId;
            if (evt.conversationId === activeConversationId && evt.message) {
                console.log(`[SignalR] Rendering message for active conversation`, evt.conversationId);
                messageThreadEl.appendChild(renderMessageCard(evt.message));
                messageThreadEl.scrollTop = messageThreadEl.scrollHeight;
            }

            const preview = (evt.message?.body || '').trim();
            const previewText = preview || (evt.message?.gifUrl ? 'GIF' : 'Message');
            const existing = activeConversationCache.get(evt.conversationId);
            const nextUnread = evt.message?.authorUserId === currentUserId
                ? (existing?.unreadCount || 0)
                : (evt.conversationId === activeConversationId ? 0 : ((existing?.unreadCount || 0) + 1));

            console.log(`[SignalR] Updating conversation list for conv-${evt.conversationId}, unread: ${nextUnread}`);
            updateConversationListItemSnapshot(evt.conversationId, {
                unreadCount: nextUnread,
                lastMessagePreview: previewText.length > 80 ? `${previewText.slice(0, 80)}…` : previewText,
                lastMessageAt: evt.message?.createdAt || new Date().toISOString()
            });

            if (isIncomingForActive) {
                scheduleMarkRead(evt.conversationId, 120);
            }

            // Trigger notification for incoming messages when not viewing that conversation
            if (typeof NotificationManager !== 'undefined' && evt.message?.authorUserId !== currentUserId && evt.conversationId !== activeConversationId) {
                const authorName = evt.message?.authorUser?.displayName || 'Someone';
                NotificationManager.handleNotification({
                    type: 'newMessage',
                    contextType: 'CsMessaging',
                    contextId: String(evt.conversationId),
                    title: `New message from ${authorName}`,
                    message: previewText.length > 60 ? `${previewText.slice(0, 60)}…` : previewText,
                    sound: true,
                    visual: true,
                    toast: true
                });
            }
        });

        connection.on('MessageReactionUpdated', async function (evt) {
            if (!evt || evt.conversationId !== activeConversationId) return;
            applyReactionUpdate(evt);
        });

        connection.on('ConversationReadUpdated', async function (evt) {
            if (!evt || evt.conversationId !== activeConversationId) return;
            markMineAsReadForDirectConversation(evt.readerUserId);
        });

        connection.on('ConversationChanged', async function () {
            await refreshConversations();
        });

        connection.on('MessageEdited', function (evt) {
            if (!evt || evt.conversationId !== activeConversationId) return;
            if (evt.message) applyMessageEdit(evt.message);
        });

        connection.on('MessageDeleted', function (evt) {
            if (!evt || evt.conversationId !== activeConversationId) return;
            applyMessageDelete(evt.messageId);
        });

        connection.on('MemberAdded', function (evt) {
            if (!evt || evt.conversationId !== activeConversationId) return;
            if (evt.member && !activeConversationMembers.some(m => m.userId === evt.member.userId)) {
                activeConversationMembers.push({ userId: evt.member.userId, displayName: evt.member.displayName });
                chatMembersEl.textContent = activeConversationMembers.map(m => m.displayName).join(', ');
                renderGroupMemberList(activeConversationMembers, activeConversationCanManage);
            }
        });

        connection.on('MemberRemoved', function (evt) {
            if (!evt || evt.conversationId !== activeConversationId) return;
            activeConversationMembers = activeConversationMembers.filter(m => m.userId !== evt.userId);
            chatMembersEl.textContent = activeConversationMembers.map(m => m.displayName).join(', ');
            renderGroupMemberList(activeConversationMembers, activeConversationCanManage);
            const listItem = document.querySelector(`#groupMemberList li[data-member-user-id="${CSS.escape(evt.userId)}"]`);
            if (listItem) listItem.remove();
        });

        connection.onreconnected(async function () {
            if (activeConversationId) {
                await safeJoinConversation(activeConversationId);
            }
            await refreshConversations();
        });

        connection.start().then(async function () {
            if (activeConversationId) {
                await safeJoinConversation(activeConversationId);
            }
        }).catch(function () {
            toast('Realtime chat connection failed.', false);
        });
    }

    if (chatSidebarEl) {
        chatSidebarEl.addEventListener('click', async function (e) {
            const conversationBtn = e.target.closest('.conversation-item');
            if (conversationBtn) {
                const id = parseInt(conversationBtn.dataset.conversationId || '0', 10);
                if (!id) return;

                if (connection && activeConversationId && activeConversationId !== id) {
                    try { await connection.invoke('LeaveConversation', activeConversationId); } catch (_) {}
                }
                await loadConversation(id);
                return;
            }

            const contactBtn = e.target.closest('.contact-item');
            if (contactBtn) {
                const userId = contactBtn.dataset.userId || '';
                if (!userId) return;
                await startDirect(userId);
            }
        });
    }

    messageThreadEl.addEventListener('click', async function (e) {
        const reactionBtn = e.target.closest('.quick-reaction, .reaction-chip');
        if (!reactionBtn) return;

        const messageId = parseInt(reactionBtn.dataset.messageId || '0', 10);
        const emoji = reactionBtn.dataset.emoji || '';
        if (!messageId || !emoji) return;

        if (emoji === '+') {
            openEmojiPicker('reaction', messageId, reactionBtn);
            return;
        }

        await toggleReaction(messageId, emoji);
    });

    if (emojiPickerBodyEl) {
        emojiPickerBodyEl.addEventListener('click', async function (e) {
            const pick = e.target.closest('.emoji-pick');
            if (!pick) return;

            const emoji = pick.dataset.emoji || '';
            if (!emoji) return;

            if (emojiPickMode === 'reaction') {
                if (reactionPickerTargetMessageId) {
                    await toggleReaction(reactionPickerTargetMessageId, emoji);
                }
            } else {
                messageBodyEl.value += emoji;
                messageBodyEl.focus();
            }

            if (emojiPickerPopoverEl) {
                emojiPickerPopoverEl.classList.add('d-none');
            }
        });
    }

    if (gifSearchBtn) {
        gifSearchBtn.addEventListener('click', searchGifs);
    }

    if (gifSearchResultsEl) {
        gifSearchResultsEl.addEventListener('click', async function (e) {
            const gifPickBtn = e.target.closest('.gif-pick');
            if (!gifPickBtn) return;

            const url = gifPickBtn.dataset.url || '';
            if (!url) return;

            const sent = await sendMessage(url);
            if (sent) {
                if (gifModal) gifModal.hide();
                gifSearchResultsEl.innerHTML = '';
                if (gifSearchInputEl) gifSearchInputEl.value = '';
                messageBodyEl.focus();
                toast('GIF sent.', true);
                return;
            }

            pastedImageUrl = url;
            pastedPreviewImgEl.src = url;
            pastedPreviewWrapEl.classList.remove('d-none');
            messageBodyEl.focus();
            if (gifModal) gifModal.hide();
            toast('GIF selected. Press Enter to send.', true);
        });
    }

    if (gifSearchInputEl) {
        gifSearchInputEl.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                searchGifs();
            }
        });
    }

    if (openEmojiPickerBtn) {
        openEmojiPickerBtn.addEventListener('click', function () {
            openEmojiPicker('compose', 0, openEmojiPickerBtn);
        });
    }

    if (closeEmojiPickerBtn && emojiPickerPopoverEl) {
        closeEmojiPickerBtn.addEventListener('click', function () {
            emojiPickerPopoverEl.classList.add('d-none');
        });
    }

    document.addEventListener('click', function (e) {
        if (!emojiPickerPopoverEl || emojiPickerPopoverEl.classList.contains('d-none')) return;

        const clickedInsidePicker = !!e.target.closest('#emojiPickerPopover');
        const clickedEmojiTrigger = !!e.target.closest('#openEmojiPickerBtn, .quick-reaction[data-emoji="+"]');
        if (clickedInsidePicker || clickedEmojiTrigger) return;

        emojiPickerPopoverEl.classList.add('d-none');
    });

    window.addEventListener('resize', function () {
        if (!emojiPickerPopoverEl || emojiPickerPopoverEl.classList.contains('d-none')) return;
        const fallbackAnchor = emojiPickMode === 'reaction'
            ? document.querySelector(`.quick-reaction[data-emoji="+"][data-message-id="${reactionPickerTargetMessageId}"]`)
            : openEmojiPickerBtn;
        positionEmojiPicker(fallbackAnchor || openEmojiPickerBtn || messageBodyEl);
    });

    if (emojiSearchInputEl) {
        emojiSearchInputEl.addEventListener('input', function () {
            emojiSearchText = emojiSearchInputEl.value || '';
            renderEmojiPicker();
        });
    }

    if (mentionSuggestionsEl) {
        mentionSuggestionsEl.addEventListener('click', function (e) {
            const option = e.target.closest('.mention-option');
            if (!option) return;

            const value = option.dataset.value || '';
            insertMentionValue(value);
        });
    }

    if (openGifPickerBtn) {
        openGifPickerBtn.addEventListener('click', function () {
            if (gifModal) gifModal.show();
        });
    }

    if (clearPastedPreviewBtn) {
        clearPastedPreviewBtn.addEventListener('click', clearPastedPreview);
    }

    if (openAttachmentBtn && attachmentInputEl) {
        openAttachmentBtn.addEventListener('click', function () {
            attachmentInputEl.click();
        });

        attachmentInputEl.addEventListener('change', async function () {
            const file = attachmentInputEl.files && attachmentInputEl.files.length
                ? attachmentInputEl.files[0]
                : null;

            if (file) {
                await uploadAttachmentFile(file);
            }

            attachmentInputEl.value = '';
        });
    }

    document.addEventListener('paste', async function (e) {
        if (!activeConversationId) return;

        const items = e.clipboardData?.items;
        if (!items || !items.length) return;

        const fileItem = Array.from(items).find(it => it.type.startsWith('image/'));
        if (!fileItem) return;

        const file = fileItem.getAsFile();
        if (!file) return;

        e.preventDefault();
        await uploadPastedFile(file);

        // Keep focus on composer so Enter immediately sends pasted media
        messageBodyEl.focus();
    });

    if (sendMessageBtn) {
        sendMessageBtn.addEventListener('click', function () { sendMessage(); });
    }

    if (startDirectBtn) {
        startDirectBtn.addEventListener('click', startDirect);
    }

    if (createGroupBtn) {
        createGroupBtn.addEventListener('click', createGroup);
    }

    if (conversationSearchEl) {
        conversationSearchEl.addEventListener('input', applyConversationSearch);
    }

    if (openConversationPaneBtn && chatSidebarEl) {
        openConversationPaneBtn.addEventListener('click', function () {
            chatSidebarEl.classList.add('show-mobile');
        });
    }

    if (closeConversationPaneBtn && chatSidebarEl) {
        closeConversationPaneBtn.addEventListener('click', function () {
            chatSidebarEl.classList.remove('show-mobile');
        });
    }

    if (messageBodyEl) {
        messageBodyEl.addEventListener('input', function () {
            updateMentionSuggestions();
        });

        messageBodyEl.addEventListener('keydown', function (e) {
            if (mentionState?.matches?.length) {
                if (e.key === 'ArrowDown') {
                    e.preventDefault();
                    moveMentionSelection(1);
                    return;
                }

                if (e.key === 'ArrowUp') {
                    e.preventDefault();
                    moveMentionSelection(-1);
                    return;
                }

                if (e.key === 'Tab' || e.key === 'Enter') {
                    if (!(e.key === 'Enter' && e.shiftKey)) {
                        e.preventDefault();
                        const selected = mentionState.matches[mentionState.selectedIndex] || mentionState.matches[0];
                        if (selected?.value) {
                            insertMentionValue(selected.value);
                        }
                        return;
                    }
                }

                if (e.key === 'Escape') {
                    e.preventDefault();
                    hideMentionSuggestions();
                    return;
                }
            }

            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });

        messageBodyEl.addEventListener('blur', function () {
            window.setTimeout(function () {
                hideMentionSuggestions();
            }, 120);
        });

        messageBodyEl.addEventListener('focus', function () {
            updateMentionSuggestions();
        });
    }

    // ── Inline edit / delete on message thread ──────────────────────────────

    messageThreadEl.addEventListener('click', async function (e) {
        // Edit button
        const editBtn = e.target.closest('.btn-msg-edit');
        if (editBtn) {
            const messageId = parseInt(editBtn.dataset.messageId || '0', 10);
            if (!messageId) return;

            const row = getMessageRow(messageId);
            if (!row) return;

            // If already in edit mode, do nothing
            if (row.querySelector('.inline-edit-form')) return;

            const bodyEl = row.querySelector('.cs-msg-body');
            if (!bodyEl) return;

            const originalText = bodyEl.textContent || '';
            const editHtml =
                `<form class="inline-edit-form d-flex gap-1 mt-1">` +
                    `<input type="text" class="form-control form-control-sm inline-edit-input" value="${escapeAttribute(originalText)}" />` +
                    `<button type="submit" class="btn btn-sm btn-primary">Save</button>` +
                    `<button type="button" class="btn btn-sm btn-secondary btn-cancel-edit">Cancel</button>` +
                `</form>`;
            bodyEl.insertAdjacentHTML('afterend', editHtml);
            const input = row.querySelector('.inline-edit-input');
            if (input) { input.focus(); input.select(); }
            return;
        }

        // Cancel inline edit
        const cancelBtn = e.target.closest('.btn-cancel-edit');
        if (cancelBtn) {
            const form = cancelBtn.closest('.inline-edit-form');
            if (form) form.remove();
            return;
        }

        // Delete button
        const deleteBtn = e.target.closest('.btn-msg-delete');
        if (deleteBtn) {
            const messageId = parseInt(deleteBtn.dataset.messageId || '0', 10);
            if (!messageId) return;

            if (!window.confirm('Delete this message?')) return;
            await deleteMessage(messageId);
            return;
        }
    });

    messageThreadEl.addEventListener('submit', async function (e) {
        const form = e.target.closest('.inline-edit-form');
        if (!form) return;
        e.preventDefault();

        const row = form.closest('.cs-msg-row');
        const messageId = row ? parseInt(row.dataset.messageId || '0', 10) : 0;
        if (!messageId) return;

        const input = form.querySelector('.inline-edit-input');
        const newBody = (input?.value || '').trim();
        if (!newBody) { toast('Message cannot be empty.', false); return; }

        await editMessage(messageId, newBody);
        form.remove();
    });

    // ── Group management modal ───────────────────────────────────────────────

    const groupInfoModalEl = document.getElementById('groupInfoModal');
    if (groupInfoModalEl) {
        groupInfoModalEl.addEventListener('show.bs.modal', function () {
            renderGroupMemberList(activeConversationMembers, activeConversationCanManage);
        });

        groupInfoModalEl.addEventListener('click', async function (e) {
            // Remove member
            const removeBtn = e.target.closest('.btn-remove-member');
            if (removeBtn) {
                const userId = removeBtn.dataset.userId || '';
                if (!userId || !activeConversationId) return;

                if (!window.confirm('Remove this member from the group?')) return;
                await removeMember(activeConversationId, userId);
                return;
            }
        });

        const addMemberBtn = document.getElementById('addMemberBtn');
        const addMemberSelect = document.getElementById('addMemberSelect');
        if (addMemberBtn && addMemberSelect) {
            addMemberBtn.addEventListener('click', async function () {
                const userId = addMemberSelect.value || '';
                if (!userId || !activeConversationId) {
                    toast('Please select a user.', false);
                    return;
                }

                const member = await addMember(activeConversationId, userId);
                if (member) {
                    toast(`${member.displayName} added to the group.`, true);
                    addMemberSelect.value = '';
                    renderGroupMemberList(activeConversationMembers, activeConversationCanManage);
                }
            });
        }
    }

    loadEmojiSetAsync().then(function () {
        renderEmojiPicker();
    });

    if (activeConversationId) {
        loadConversation(activeConversationId);
    }
})();
