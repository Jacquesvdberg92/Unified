/**
 * cslivehelp.js
 * Shared real-time SignalR client for all three CS Live Help pages.
 * 
 * ARCHITECTURE REFERENCE: See docs/CsLiveHelp-Architecture.md for:
 * - SignalR group routing (am-{userId} vs cs-board)
 * - Event flow and real-time update behavior
 * - Card partial endpoints and live modal injection
 * - AM comment thread refresh behavior (AmCommentThread endpoint)
 * 
 * Page-specific configuration:
 *   window.csCardPartialUrl   — URL prefix for live card fetches (set per page)
 *   window.csModalsPartialUrl — URL prefix for live modal fetches (set per page)
 *
 * Events received:
 *   CardAdded          { id, brandName, requestType, status, assignedTo, isInternal }
 *   CardUpdated        { id, brandName, requestType, status, assignedTo }
 *   CardStatusChanged  { id, newStatus, assignedTo }
 *   CardDeleted        { id }
 *   CommentAdded       { requestId, author, body, isSystem, createdAt }
 *
 * Load-more:
 *   Each column has a "Load more" button with data-status and data-after-id.
 *   Clicking it fetches /CsLiveHelp/BoardPage?status=X&afterId=Y and appends cards.
 */
(function () {
    'use strict';

    // ── Helpers ──────────────────────────────────────────────────────────────

    function colEl(status) {
        return document.getElementById('col-' + status);
    }

    function cardEl(id) {
        return document.querySelector('[data-card-id="' + id + '"]');
    }

    function removeEmptyHint(col) {
        col?.querySelectorAll('.empty-col-hint').forEach(el => el.remove());
    }

    function addEmptyHintIfEmpty(col) {
        if (col && col.querySelectorAll('[data-card-id]').length === 0 && !col.querySelector('.empty-col-hint')) {
            const p = document.createElement('p');
            p.className = 'text-muted small text-center mt-3 empty-col-hint';
            p.textContent = 'None';
            col.appendChild(p);
        }
    }

    function updateColCount(status) {
        const col   = colEl(status);
        const badge = document.getElementById('count-' + status);
        if (col && badge) badge.textContent = col.querySelectorAll('[data-card-id]').length;
    }

    const badgeClass = {
        Open:       'bg-secondary',
        InProgress: 'bg-warning text-dark',
        OnGoing:    'bg-warning text-dark',
        Escalated:  'bg-danger',
        Completed:  'bg-success'
    };

    function showToastMsg(msg, success) {
        if (typeof showToast === 'function') { showToast(msg, success); return; }
        console[success ? 'info' : 'warn']('[CsLiveHelp]', msg);
    }

    function statusLabel(status) {
        return status === 'Escalated' ? 'Passed to CS' : status;
    }

    function renderBroadcastBanner(data) {
        const host = document.getElementById('csBroadcastBannerHost');
        if (!host) return;

        const msg = (data?.message ?? '').toString().trim();
        if (!msg) return;

        const actor = (data?.actor ?? 'System').toString();
        const dt = new Date(data?.timestamp);
        const ts = isNaN(dt.getTime()) ? 'now' : dt.toLocaleString();

        host.style.display = '';
        host.innerHTML =
            '<div class="alert alert-warning alert-dismissible fade show mb-0" role="alert">' +
                '<div class="d-flex align-items-start gap-2">' +
                    '<i class="bx bx-bell mt-1"></i>' +
                    '<div>' +
                        '<div class="fw-semibold">Broadcast Reminder</div>' +
                        '<div>' + escHtml(msg) + '</div>' +
                        '<div class="small text-muted mt-1">From ' + escHtml(actor) + ' · ' + escHtml(ts) + '</div>' +
                    '</div>' +
                '</div>' +
                '<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>' +
            '</div>';
    }

    function escHtml(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function ensureCardModals(id) {
        if (document.getElementById('statusModal-' + id) ||
            document.getElementById('csCommentModal-' + id) ||
            document.getElementById('commentModal-' + id)) {
            return Promise.resolve();
        }

        const modalsUrl = (window.csModalsPartialUrl ?? '/CsLiveHelp/CardModalsPartial/') + id;
        return fetch(modalsUrl, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
        .then(function (res) {
            if (!res.ok) throw new Error('modals partial failed');
            return res.text();
        })
        .then(function (html) {
            const wrap = document.createElement('div');
            wrap.innerHTML = html.trim();
            while (wrap.firstElementChild) {
                document.body.appendChild(wrap.firstElementChild);
            }
        })
        .catch(function () {
            // keep card visible even if modal markup fails
        });
    }

    const hasAnyBoardColumn = !!document.querySelector('[id^="col-"]');
    const hasEscalatedColumn = !!document.getElementById('col-Escalated');

    /**
     * Map status to the correct column for display.
     * On AM Requests page: Escalated → InProgress (no separate Escalated column)
     * On CS Board and All Brands: status → status (all columns exist)
     * 
     * If the column doesn't exist, log a warning (helps catch stale state or bugs).
     */
    function statusColumn(status) {
        // On the AM Requests page (Requests.cshtml), there is no separate Escalated column.
        // Escalated cards should appear in the "In Progress / Escalated" column.
        // Note: OnGoing status also doesn't exist on AM page, but is rarely emitted for AM cards.
        const mappedStatus = (!hasEscalatedColumn && status === 'Escalated') ? 'InProgress' : status;

        const colEl = document.getElementById('col-' + mappedStatus);
        if (!colEl && hasAnyBoardColumn) {
            // The column element is missing; warn for debugging
            console.warn('[CsLiveHelp] Column col-' + mappedStatus + ' not found (status=' + status + ')');
        }

        return mappedStatus;
    }

    // ── SignalR connection ───────────────────────────────────────────────────

    const seenNotificationKeys = new Set();

    function markSeen(key) {
        if (!key) return false;
        if (seenNotificationKeys.has(key)) return true;
        seenNotificationKeys.add(key);
        if (seenNotificationKeys.size > 500) {
            const first = seenNotificationKeys.values().next().value;
            if (first) seenNotificationKeys.delete(first);
        }
        return false;
    }

     const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/cslivehelp')
        .withAutomaticReconnect()
        .build();

    // ── Connection state tracking for debugging and recovery ──────────────────

    window.csConnectionState = {
        isConnected: false,
        attemptedCount: 0,
        lastError: null,
        connectedAt: null,
        disconnectedAt: null
    };

    connection.onreconnecting((err) => {
        window.csConnectionState.lastError = err?.message || 'Unknown error';
        window.csConnectionState.attemptedCount++;
        const msg = `Board reconnecting (attempt ${window.csConnectionState.attemptedCount})…`;
        console.warn('[CsLiveHelp]', msg, err);
        showToastMsg(msg, false);
    });

    connection.onreconnected(() => {
        window.csConnectionState.isConnected = true;
        window.csConnectionState.connectedAt = new Date();
        window.csConnectionState.attemptedCount = 0;
        window.csConnectionState.lastError = null;
        console.log('[CsLiveHelp] SignalR connection restored');
        showToastMsg('Board reconnected.', true);
        // Optionally: fetch updated card state here if needed (uncomment if desired)
    });

    connection.onclose((err) => {
        window.csConnectionState.isConnected = false;
        window.csConnectionState.disconnectedAt = new Date();
        window.csConnectionState.lastError = err?.message || 'Unknown error';
        console.warn('[CsLiveHelp] SignalR connection closed:', err);
    });

    // ── Event: CardStatusChanged ─────────────────────────────────────────────

    connection.on('CardStatusChanged', function (data) {
        const card = cardEl(data.id);
        if (!card) return;

        const newStatus = data.newStatus;
        const targetCol = colEl(statusColumn(newStatus));
        // Use [data-status] so this works on both the CS board (kanban-col) and
        // the AM Requests page (column containers tagged with data-status only).
        const sourceCol = card.closest('[data-status]');

        if (targetCol && sourceCol !== targetCol) {
            const oldStatus = sourceCol?.dataset.status;
            removeEmptyHint(targetCol);
            targetCol.appendChild(card);
            addEmptyHintIfEmpty(sourceCol);
            updateColCount(newStatus);
            if (oldStatus) updateColCount(oldStatus);
        }

        const badge = card.querySelector('.badge');
        if (badge && badgeClass[newStatus]) {
            badge.className = 'badge ' + badgeClass[newStatus];
            badge.textContent = statusLabel(newStatus);
        }

        if (data.assignedTo) {
            const assignedEl = card.querySelector('.cs-assigned-name');
            if (assignedEl) {
                assignedEl.textContent = data.assignedTo;
                const row = card.querySelector('.cs-assigned-row');
                if (row) row.style.display = '';
            }
        }
    });

    // ── Event: CardAdded ─────────────────────────────────────────────────────

    connection.on('CardAdded', function (data) {
        const col = colEl(statusColumn(data.status ?? 'Open'));
        if (!col) return;

        removeEmptyHint(col);

        const partialUrl = (window.csCardPartialUrl ?? '/CsLiveHelp/CardPartial/') + data.id;
        fetch(partialUrl, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
        .then(function (res) {
            if (!res.ok) throw new Error('partial failed');
            return res.text();
        })
        .then(function (html) {
            const wrap = document.createElement('div');
            wrap.innerHTML = html.trim();
            const card = wrap.firstElementChild;
            if (card) {
                card.classList.add('new-card-notice');
                col.prepend(card);
            }
            updateColCount(statusColumn(data.status ?? 'Open'));
            ensureCardModals(data.id).finally(function () {
                wireCommentModal(data.id);
                wireCsCommentModal(data.id);
                showToastMsg('New card #' + data.id + ' added (' + escHtml(data.brandName ?? '') + ').', true);
            });
        })
        .catch(function () {
            const div = document.createElement('div');
            div.className = 'card shadow-sm border-0 new-card-notice';
            div.dataset.cardId = data.id;
            div.innerHTML =
                '<div class="card-body py-2 px-3 d-flex justify-content-between align-items-center">' +
                    '<span class="small fw-semibold">#' + data.id + ' &mdash; ' + escHtml(data.brandName ?? '') +
                    ' &middot; ' + escHtml(data.requestType ?? '') +
                    (data.isInternal ? ' <span class="badge bg-info text-dark ms-1">Internal</span>' : '') + '</span>' +
                    '<span class="badge ' + (badgeClass[data.status] ?? 'bg-secondary') + '">' + escHtml(statusLabel(data.status ?? 'Open')) + '</span>' +
                '</div>' +
                '<div class="card-footer py-1 px-3 text-muted small">New card &mdash; ' +
                '<a href="" onclick="location.reload();return false;">refresh</a> for full actions</div>';
            col.prepend(div);
            updateColCount(statusColumn(data.status ?? 'Open'));
            ensureCardModals(data.id).finally(function () {
                wireCommentModal(data.id);
                wireCsCommentModal(data.id);
                showToastMsg('New card #' + data.id + ' added (' + escHtml(data.brandName ?? '') + ').', true);
            });
        });
    });

    // ── Event: CardUpdated ───────────────────────────────────────────────────

    connection.on('CardUpdated', function (data) {
        showToastMsg('Card #' + data.id + ' was updated.', true);
        const card = cardEl(data.id);
        if (card) {
            card.classList.add('border-warning');
            setTimeout(() => card.classList.remove('border-warning'), 3000);
        }
    });

    // ── Event: CardDeleted ───────────────────────────────────────────────────

    connection.on('CardDeleted', function (data) {
        const card = cardEl(data.id);
        if (!card) return;

        const col = card.closest('.kanban-col');
        const deletedStatus = col?.dataset.status;
        card.remove();
        ['statusModal-', 'csCommentModal-', 'escalateModal-', 'resetModal-', 'passedModal-']
            .forEach(function (prefix) {
                const el = document.getElementById(prefix + data.id);
                if (el) el.remove();
            });
        addEmptyHintIfEmpty(col);
        if (deletedStatus) updateColCount(deletedStatus);
        showToastMsg('Card #' + data.id + ' was deleted.', true);
    });

    // ── Event: CommentAdded ──────────────────────────────────────────────────

    /**
     * Hardened thread body detection that tries all known modal patterns.
     * Returns the thread-body element if found, or null.
     * Works across all three pages: Board, Requests, and RequestsAllBrands.
     * 
     * NOTE: Do NOT rely on visibility checks (offsetParent). The thread body
     * may be inside a hidden modal, but we still need to append comments to it.
     */
    function findThreadBodyElement(requestId) {
        // Pattern 1: CS Board comment modal with .thread-body (csCommentModal-{id} .thread-body)
        let threadBody = document.querySelector('#csCommentModal-' + requestId + ' .thread-body');
        if (threadBody) return threadBody;

        // Pattern 2: Internal board comment modal with .thread-body (intCommentModal-{id} .thread-body)
        threadBody = document.querySelector('#intCommentModal-' + requestId + ' .thread-body');
        if (threadBody) return threadBody;

        // Pattern 3: AM Requests comment modal with .thread-body (commentModal-{id} .thread-body)
        threadBody = document.querySelector('#commentModal-' + requestId + ' .thread-body');
        if (threadBody) return threadBody;

        // Pattern 4: Internal board comment modal with .border.rounded fallback (intCommentModal-{id} .modal-body .border.rounded)
        threadBody = document.querySelector('#intCommentModal-' + requestId + ' .modal-body .border.rounded');
        if (threadBody) return threadBody;

        // Pattern 5: Fallback "No comments yet" placeholder as <p id="threadBody-{id}">
        threadBody = document.getElementById('threadBody-' + requestId);
        if (threadBody && (threadBody.tagName === 'P' || threadBody.tagName === 'DIV')) return threadBody;

        // Pattern 6: If no .thread-body exists, look for .modal-body directly
        // This is a fallback for modals that may not have a dedicated thread container yet
        const modal = document.getElementById('csCommentModal-' + requestId) 
            || document.getElementById('commentModal-' + requestId)
            || document.getElementById('intCommentModal-' + requestId);

        if (modal) {
            const modalBody = modal.querySelector('.modal-body');
            if (modalBody) {
                // Create a thread body if one doesn't exist
                threadBody = document.createElement('div');
                threadBody.className = 'thread-body mb-3 border rounded p-2 small';
                threadBody.style.cssText = 'max-height:260px;overflow-y:auto';
                threadBody.id = 'threadBody-' + requestId;

                // Insert before the textarea or at the start of modal-body
                const textarea = modalBody.querySelector('textarea');
                if (textarea) {
                    modalBody.insertBefore(threadBody, textarea.parentElement);
                } else {
                    modalBody.insertBefore(threadBody, modalBody.firstChild);
                }
                return threadBody;
            }
        }

        return null;
    }

    connection.on('CommentAdded', function (data) {
        const requestId = data.requestId;

        // Update card comment count badge (supports both .comment-count and .int-comment-count)
        const card = cardEl(requestId);
        if (card) {
            // Try both selectors: .comment-count (Board/Requests) and .int-comment-count (Internal)
            let countBadge = card.querySelector('.comment-count');
            if (!countBadge) countBadge = card.querySelector('.int-comment-count');

            if (countBadge) {
                // Badge exists: increment it
                const n = parseInt(countBadge.textContent, 10) || 0;
                countBadge.textContent = n + 1;
                // Show comment count link if it was hidden (no previous comments)
                const countBtn = card.querySelector('.comment-count-btn');
                if (!countBtn) {
                    // For internal cards that may not have comment-count-btn initially
                    const commentBtn = card.querySelector('[data-bs-target*="CommentModal"]');
                    if (commentBtn) commentBtn.style.display = '';
                }
                if (countBtn) countBtn.style.display = '';
            } else {
                // Badge doesn't exist: first comment being added
                // Need to create the comment count button and badge
                const cardBody = card.querySelector('.card-body') || card;
                if (cardBody) {
                    // Find where to insert (after description/assignment info, before timestamp)
                    let insertAfter = cardBody.querySelector('[style*="font-size:.72rem"]');
                    if (!insertAfter) insertAfter = cardBody.querySelector('.text-muted');

                    if (!insertAfter) {
                        // If no timestamp found, insert at the end of card body
                        insertAfter = cardBody.lastElementChild;
                    }

                    if (insertAfter) {
                        const buttonDiv = document.createElement('div');
                        buttonDiv.className = 'mt-1';
                        const targetModalId = card.querySelector('[data-bs-target*="CommentModal"]')?.getAttribute('data-bs-target') || '#commentModal-' + requestId;
                        buttonDiv.innerHTML = '<button class="btn btn-link btn-sm p-0 text-muted comment-count-btn" style="font-size:.72rem" data-bs-toggle="modal" data-bs-target="' + targetModalId + '" title="View thread"><i class="bx bx-comment me-1"></i><span class="comment-count">1</span> comment(s) — View thread</button>';

                        insertAfter.parentElement.insertBefore(buttonDiv, insertAfter);
                        console.log('[CsLiveHelp] Created comment count button for first comment on request', requestId);
                    }
                }
            }
        }

        // Find or create thread body element
        let threadBody = findThreadBodyElement(requestId);

        if (!threadBody) {
            // If findThreadBodyElement() failed to create one (no modal in DOM yet),
            // we'll just skip this comment. It will appear when the modal is opened.
            console.debug('[CsLiveHelp] CommentAdded: thread body not available yet for request', requestId, '(modal may not be open)');
            return;
        }

        // Ensure threadBody is a proper element (not a string or phantom)
        if (!threadBody.appendChild) {
            console.warn('[CsLiveHelp] CommentAdded: thread body is not a valid element for request', requestId);
            return;
        }

        // Upgrade placeholder if this is the first real comment
        if (threadBody.tagName === 'P') {
            const wrap = document.createElement('div');
            wrap.className = 'thread-body mb-3 border rounded p-2 small';
            wrap.style.cssText = 'max-height:260px;overflow-y:auto';
            wrap.id = 'threadBody-' + requestId;
            threadBody.replaceWith(wrap);
            threadBody = wrap;
        }

        // Remove "No comments yet" placeholder if present
        const emptyP = threadBody.querySelector('p.text-muted');
        if (emptyP) emptyP.remove();

        // Build and append comment HTML
        const dt = new Date(data.createdAt);
        const ts = isNaN(dt.getTime()) ? '(timestamp unavailable)' : dt.toLocaleString();
        const div = document.createElement('div');
        div.className = 'mb-2 p-2 rounded ' + (data.isSystem ? 'bg-warning bg-opacity-10' : 'bg-light');
        div.innerHTML =
            '<div class="d-flex justify-content-between mb-1">' +
                '<strong class="small">' + escHtml(data.author) + '</strong>' +
                '<span class="text-muted small">' + escHtml(ts) + '</span>' +
            '</div>' +
            '<p class="mb-0 small">' + escHtml(data.body) +
            (data.isSystem ? ' <span class="badge bg-warning text-dark ms-1">System</span>' : '') + '</p>';

        // Add image/attachment if provided
        if (data.imagePath) {
            div.innerHTML +=
                '<div class="mt-2"><a href="' + escHtml(data.imagePath) + '" target="_blank" rel="noopener">' +
                '<img src="' + escHtml(data.imagePath) + '" alt="Attachment" class="img-fluid rounded" style="max-height:180px" />' +
                '</a></div>';
        }

        threadBody.appendChild(div);
        threadBody.scrollTop = threadBody.scrollHeight;

        console.log('[CsLiveHelp] Comment added to request', requestId, '— author:', data.author);
    });

    // ── Notification Events ──────────────────────────────────────────────────

    /**
     * Handle request notifications (new request, escalation, etc.)
     */
    connection.on('RequestNotification', function (data) {
        if (typeof NotificationManager === 'undefined') {
            console.log('[CsLiveHelp] NotificationManager not available; skipping notification');
            return;
        }

        const contextType = data.contextType || 'Board';
        const contextId = String(data.requestId || '');
        const dedupeKey = `request:${data.type || 'update'}:${contextId}:${data.timestamp || ''}`;
        if (markSeen(dedupeKey)) return;

        const notificationTitle = data.type === 'newRequest' ? 'New Request' :
                                  data.type === 'escalated' ? 'Request Escalated' : 'Request Update';
        const notificationMessage = data.brandName
            ? `${data.brandName}${data.requestType ? ` - ${data.requestType}` : ''}`
            : 'New request created';

        const shouldNotify = !NotificationManager.isMuted?.(contextType, contextId);

        if (shouldNotify) {
            try {
                NotificationManager.handleNotification({
                    type: data.type,
                    contextType: contextType,
                    contextId: contextId,
                    title: notificationTitle,
                    message: notificationMessage,
                    sound: true,
                    visual: true,
                    toast: true,
                    callback: function (result) {
                        console.log(`[CsLiveHelp] ${notificationTitle}:`, data, 'Sound played:', result.playedSound);
                    }
                });
            } catch (err) {
                console.warn('[CsLiveHelp] NotificationManager.handleNotification failed:', err);
            }
        }
    });

    /**
     * Handle comment notifications
     */
    connection.on('CommentNotification', function (data) {
        if (typeof NotificationManager === 'undefined') {
            console.log('[CsLiveHelp] NotificationManager not available; skipping notification');
            return;
        }

        const contextType = data.contextType || 'Board';
        const contextId = String(data.requestId || '');
        const dedupeKey = `comment:${contextId}:${data.author || ''}:${data.timestamp || ''}`;
        if (markSeen(dedupeKey)) return;

        const notificationTitle = 'New Comment';
        const notificationMessage = `${data.author} commented on request #${contextId}`;

        const shouldNotify = !NotificationManager.isMuted?.(contextType, contextId);

        if (shouldNotify) {
            try {
                NotificationManager.handleNotification({
                    type: 'comment',
                    contextType: contextType,
                    contextId: contextId,
                    title: notificationTitle,
                    message: notificationMessage,
                    sound: true,
                    visual: true,
                    toast: true,
                    callback: function (result) {
                        console.log(`[CsLiveHelp] Comment from ${data.author}:`, data, 'Sound played:', result.playedSound);
                    }
                });
            } catch (err) {
                console.warn('[CsLiveHelp] NotificationManager.handleNotification failed:', err);
            }
        }
    });

    /**
     * Handle mention notifications (internal comments with mentions)
     */
    connection.on('MentionNotification', function (data) {
        if (typeof NotificationManager === 'undefined') {
            console.log('[CsLiveHelp] NotificationManager not available; skipping notification');
            return;
        }

        const contextType = data.contextType || 'RequestsAllBrands';
        const contextId = String(data.requestId || '');
        const dedupeKey = `mention:${contextId}:${data.author || ''}:${data.timestamp || ''}`;
        if (markSeen(dedupeKey)) return;

        const notificationTitle = 'You were mentioned';
        const notificationMessage = `${data.author} mentioned you in request #${contextId}`;

        const shouldNotify = !NotificationManager.isMuted?.(contextType, contextId);

        if (shouldNotify) {
            try {
                NotificationManager.handleNotification({
                    type: 'mention',
                    contextType: contextType,
                    contextId: contextId,
                    title: notificationTitle,
                    message: notificationMessage,
                    sound: true,
                    visual: true,
                    toast: true,
                    callback: function (result) {
                        console.log(`[CsLiveHelp] Mention from ${data.author}:`, data, 'Sound played:', result.playedSound);
                    }
                });
            } catch (err) {
                console.warn('[CsLiveHelp] NotificationManager.handleNotification failed:', err);
            }
        }
    });

    connection.on('BroadcastBanner', function (data) {
        const dedupeKey = `broadcast:${data?.message || ''}:${data?.timestamp || ''}`;
        if (markSeen(dedupeKey)) return;

        renderBroadcastBanner(data);

        if (typeof NotificationManager !== 'undefined') {
            try {
                NotificationManager.handleNotification({
                    type: 'broadcast',
                    contextType: 'Board',
                    contextId: 'cs-live-help-broadcast',
                    title: 'Broadcast Reminder',
                    message: data?.message || 'New reminder received.',
                    sound: true,
                    visual: true,
                    toast: true
                });
            } catch (err) {
                console.warn('[CsLiveHelp] Broadcast notification failed:', err);
            }
        }
    });

    connection.start()
        .then(() => {
            window.csConnectionState.isConnected = true;
            window.csConnectionState.connectedAt = new Date();
            console.log('[CsLiveHelp] SignalR connection established');
        })
        .catch(err => {
            window.csConnectionState.isConnected = false;
            window.csConnectionState.lastError = err?.message || 'Connection failed';
            console.error('[CsLiveHelp] SignalR connection failed:', err);
        });

    // Copy client ID helper for CS/internal cards
    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.copy-client-id-btn');
        if (!btn) return;

        const clientId = btn.dataset.clientId;
        if (!clientId) return;

        try {
            if (navigator.clipboard?.writeText) {
                await navigator.clipboard.writeText(clientId);
            } else {
                const ta = document.createElement('textarea');
                ta.value = clientId;
                ta.setAttribute('readonly', '');
                ta.style.position = 'absolute';
                ta.style.left = '-9999px';
                document.body.appendChild(ta);
                ta.select();
                document.execCommand('copy');
                ta.remove();
            }

            showToastMsg('Client ID copied.', true);
        } catch (_) {
            showToastMsg('Failed to copy Client ID.', false);
        }
    });

    // ── AM Requests page: AJAX form submissions ──────────────────────────────
    //
    // Intercept form submissions for: createModal, editModal-*, deleteModal-*,
    // commentModal-* (reply form). On success the SignalR events update the DOM;
    // we just close the modal and show a toast.

    /**
     * Set up AJAX handling for a form element.
     * Prevents duplicate submissions by disabling the button during the request.
     * Uses a flag to track if a submission is already in flight.
     */
    function ajaxFormSetup(formEl, modalEl) {
        if (formEl.dataset.ajaxWired) return;
        formEl.dataset.ajaxWired = '1';
        formEl.dataset.isSubmitting = '0';

        formEl.addEventListener('submit', async function (e) {
            e.preventDefault();

            // Prevent duplicate submissions
            if (formEl.dataset.isSubmitting === '1') {
                console.warn('[CsLiveHelp] Form submission already in progress');
                return;
            }

            formEl.dataset.isSubmitting = '1';

            const fd   = new FormData(formEl);
            // Defensive file handling: explicitly include selected files for dynamic modal forms.
            formEl.querySelectorAll('input[type="file"][name]').forEach(function (fileInput) {
                const file = fileInput.files?.[0];
                if (file) fd.set(fileInput.name, file, file.name);
            });

            const url  = formEl.action || formEl.getAttribute('action');
            const btn  = formEl.querySelector('[type="submit"]');
            const originalText = btn?.innerHTML;
            if (btn) { 
                btn.disabled = true;
                btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Submitting…';
            }

            try {
                const res = await fetch(url, {
                    method: 'POST',
                    body: fd,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                let data = {};
                try { data = await res.json(); } catch (_) { /* non-JSON body */ }

                if (res.ok && data.success !== false) {
                    // Close the modal
                    if (modalEl) {
                        const bsModal = bootstrap.Modal.getInstance(modalEl);
                        if (bsModal) bsModal.hide();
                    }
                    formEl.reset();
                    showToastMsg(data.message ?? 'Done.', true);
                    console.log('[CsLiveHelp] Form submitted successfully:', url);
                } else {
                    showToastMsg(data.error ?? 'An error occurred. Please try again.', false);
                    console.warn('[CsLiveHelp] Form submission error:', data);
                }
            } catch (err) {
                showToastMsg('Network error. Please try again.', false);
                console.error('[CsLiveHelp] Form submission network error:', err);
            } finally {
                formEl.dataset.isSubmitting = '0';
                if (btn) { 
                    btn.disabled = false;
                    if (originalText) btn.innerHTML = originalText;
                }
            }
        });
    }

    function wireCommentModal(id) {
        const modal = document.getElementById('commentModal-' + id);
        if (!modal) return;
        const form = modal.querySelector('form[id^="commentForm-"]') ?? modal.querySelector('form');
        if (form && !form.dataset.ajaxWired) ajaxFormSetup(form, modal);
    }

    function wireCsCommentModal(id) {
        const modal = document.getElementById('csCommentModal-' + id);
        if (!modal) return;
        const form = modal.querySelector('form');
        if (form && !form.dataset.ajaxWired) ajaxFormSetup(form, modal);
    }

    function wireAmForms() {
        // Create modal
        const createModal = document.getElementById('createModal');
        const createForm  = createModal?.querySelector('form');
        if (createForm && createModal) ajaxFormSetup(createForm, createModal);

        // Edit modals  (editModal-*)
        document.querySelectorAll('[id^="editModal-"]').forEach(function (modal) {
            const form = modal.querySelector('form');
            if (form) ajaxFormSetup(form, modal);
        });

        // Delete confirm modals (deleteModal-*)
        document.querySelectorAll('[id^="deleteModal-"]').forEach(function (modal) {
            const form = modal.querySelector('form');
            if (form) ajaxFormSetup(form, modal);
        });

        // Comment reply forms inside commentModal-*
        document.querySelectorAll('[id^="commentModal-"]').forEach(function (modal) {
            const form = modal.querySelector('form[id^="commentForm-"]');
            if (form) ajaxFormSetup(form, modal);
        });
    }

    wireAmForms();

    if (!hasAnyBoardColumn) return;

    function wireCsCommentModals() {
        // Wire all csCommentModal-* forms (CS Board page)
        document.querySelectorAll('[id^="csCommentModal-"]').forEach(function (modal) {
            const form = modal.querySelector('form');
            if (form) ajaxFormSetup(form, modal);
        });
        // Wire all intCommentModal-* forms (All Brands internal page)
        document.querySelectorAll('[id^="intCommentModal-"]').forEach(function (modal) {
            const form = modal.querySelector('form');
            if (form) ajaxFormSetup(form, modal);
        });
    }

    wireCsCommentModals();

    function cleanupStaleModalState() {
        const openModals = document.querySelectorAll('.modal.show').length;
        const backdrops = document.querySelectorAll('.modal-backdrop');

        if (openModals === 0) {
            document.body.classList.remove('modal-open');
            document.body.style.removeProperty('padding-right');
            backdrops.forEach(function (b) { b.remove(); });
            return;
        }

        if (backdrops.length > openModals) {
            backdrops.forEach(function (b, i) {
                if (i < backdrops.length - openModals) b.remove();
            });
        }
    }

    // Stabilize repeated open/close for comment modals (prevents stuck overlay/loading state)
    document.addEventListener('show.bs.modal', function (e) {
        const modal = e.target;
        if (!modal?.id) return;

        const isCommentModal = modal.id.startsWith('commentModal-')
            || modal.id.startsWith('csCommentModal-')
            || modal.id.startsWith('intCommentModal-');

        if (!isCommentModal) return;

        cleanupStaleModalState();

        // DO NOT FETCH THREAD CONTENT — it causes race conditions and loading loops
        // The thread is already rendered in the initial HTML from the server.
        // SignalR CommentAdded events will append new comments to the existing thread.
        // Fetching creates a race condition where the element gets replaced mid-operation.
    }, false);

    // Use 'hide.bs.modal' (fires BEFORE aria-hidden applied) not 'hidden.bs.modal' (fires AFTER)
    // This ensures we clear focus and prevent accessibility violations
    document.addEventListener('hide.bs.modal', function (e) {
        const modal = e.target;
        if (!modal?.id) return;

        const isCommentModal = modal.id.startsWith('commentModal-')
            || modal.id.startsWith('csCommentModal-')
            || modal.id.startsWith('intCommentModal-');

        if (!isCommentModal) return;

        // Clear focus BEFORE Bootstrap applies aria-hidden
        // This prevents the accessibility violation
        const focusedEl = modal.querySelector(':focus');
        if (focusedEl) {
            focusedEl.blur();
        }
    });

    document.addEventListener('hidden.bs.modal', function (e) {
        const modal = e.target;
        if (!modal?.id) return;

        const isCommentModal = modal.id.startsWith('commentModal-')
            || modal.id.startsWith('csCommentModal-')
            || modal.id.startsWith('intCommentModal-');

        if (!isCommentModal) return;

        cleanupStaleModalState();
    });

    // ── Load-more ────────────────────────────────────────────────────────────

    document.querySelectorAll('.load-more-btn').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            const status  = btn.dataset.status;
            const afterId = btn.dataset.afterId ?? '0';
            const col     = colEl(status);
            if (!col) return;

            btn.disabled = true;
            btn.textContent = 'Loading\u2026';

            try {
                const res = await fetch(
                    '/CsLiveHelp/BoardPage?status=' + encodeURIComponent(status) + '&afterId=' + afterId,
                    { headers: { 'X-Requested-With': 'XMLHttpRequest' } }
                );

                if (!res.ok) { btn.textContent = 'Error \u2014 try again'; btn.disabled = false; return; }

                const html = await res.text();
                const wrap = document.createElement('div');
                wrap.innerHTML = html;

                const cards = wrap.querySelectorAll('[data-card-id]');
                if (cards.length === 0) { btn.textContent = 'No more cards'; return; }

                const btnRow = btn.closest('.load-more-row') ?? btn.parentElement;
                cards.forEach(c => col.insertBefore(c, btnRow));

                const ids = [...cards].map(c => parseInt(c.dataset.cardId, 10)).filter(n => !isNaN(n));
                if (ids.length > 0) btn.dataset.afterId = Math.min(...ids);

                removeEmptyHint(col);
                btn.disabled = false;
                btn.textContent = 'Load more';

                if (cards.length < 50) { btn.textContent = 'No more cards'; btn.disabled = true; }
            } catch (_) {
                btn.textContent = 'Error \u2014 try again';
                btn.disabled = false;
            }
        });
    });
})();
