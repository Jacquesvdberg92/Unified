/**
 * cslivehelp.js
 * Real-time SignalR client for the CS Live Help Kanban board.
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

    function escHtml(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function ensureCardModals(id) {
        if (document.getElementById('statusModal-' + id) || document.getElementById('csCommentModal-' + id)) {
            return Promise.resolve();
        }

        return fetch('/CsLiveHelp/CardModalsPartial/' + id, {
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

    // ── SignalR connection ───────────────────────────────────────────────────

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/cslivehelp')
        .withAutomaticReconnect()
        .build();

    // ── Event: CardStatusChanged ─────────────────────────────────────────────

    connection.on('CardStatusChanged', function (data) {
        const card = cardEl(data.id);
        if (!card) return;

        const newStatus = data.newStatus;
        const targetCol = colEl(newStatus);
        const sourceCol = card.closest('.kanban-col');

        if (targetCol && sourceCol !== targetCol) {
            removeEmptyHint(targetCol);
            targetCol.appendChild(card);
            addEmptyHintIfEmpty(sourceCol);
        }

        const badge = card.querySelector('.badge');
        if (badge && badgeClass[newStatus]) {
            badge.className = 'badge ' + badgeClass[newStatus];
            badge.textContent = newStatus;
        }

        if (data.assignedTo) {
            const assignedEl = card.querySelector('.cs-assigned-name');
            if (assignedEl) assignedEl.textContent = data.assignedTo;
        }
    });

    // ── Event: CardAdded ─────────────────────────────────────────────────────

    connection.on('CardAdded', function (data) {
        const col = colEl(data.status ?? 'Open');
        if (!col) return;

        removeEmptyHint(col);

        fetch('/CsLiveHelp/CardPartial/' + data.id, {
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
            ensureCardModals(data.id).finally(function () {
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
                    '<span class="badge ' + (badgeClass[data.status] ?? 'bg-secondary') + '">' + escHtml(data.status ?? 'Open') + '</span>' +
                '</div>' +
                '<div class="card-footer py-1 px-3 text-muted small">New card &mdash; ' +
                '<a href="" onclick="location.reload();return false;">refresh</a> for full actions</div>';
            col.prepend(div);
            ensureCardModals(data.id).finally(function () {
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
        card.remove();
        ['statusModal-', 'csCommentModal-', 'escalateModal-', 'resetModal-', 'passedModal-']
            .forEach(function (prefix) {
                const el = document.getElementById(prefix + data.id);
                if (el) el.remove();
            });
        addEmptyHintIfEmpty(col);
        showToastMsg('Card #' + data.id + ' was deleted.', true);
    });

    // ── Event: CommentAdded ──────────────────────────────────────────────────

    connection.on('CommentAdded', function (data) {
        const card = cardEl(data.requestId);
        if (card) {
            const countBadge = card.querySelector('.comment-count');
            if (countBadge) {
                const n = parseInt(countBadge.textContent, 10) || 0;
                countBadge.textContent = n + 1;
            }
            // Show comment count link if it was hidden (no previous comments)
            const countBtn = card.querySelector('.comment-count-btn');
            if (countBtn) countBtn.style.display = '';
        }

        // Target both CS board thread modals (#threadModal-N) and AM comment modals (#commentModal-N .thread-body)
        const threadBody = document.querySelector('#threadModal-' + data.requestId + ' .thread-body')
                        ?? document.querySelector('#commentModal-' + data.requestId + ' .thread-body')
                        ?? document.querySelector('#csCommentModal-' + data.requestId + ' .thread-body')
                        ?? document.querySelector('#intCommentModal-' + data.requestId + ' .modal-body .border.rounded');
        if (threadBody) {
            const emptyP = threadBody.querySelector('p.text-muted');
            if (emptyP) emptyP.remove();
            const dt = new Date(data.createdAt);
            const ts = isNaN(dt.getTime()) ? '' : dt.toLocaleString();
            const div = document.createElement('div');
            div.className = 'mb-2 p-2 rounded ' + (data.isSystem ? 'bg-warning bg-opacity-10' : 'bg-light');
            div.innerHTML =
                '<div class="d-flex justify-content-between mb-1">' +
                    '<strong class="small">' + escHtml(data.author) + '</strong>' +
                    '<span class="text-muted small">' + escHtml(ts) + '</span>' +
                '</div>' +
                '<p class="mb-0 small">' + escHtml(data.body) +
                (data.isSystem ? ' <span class="badge bg-warning text-dark ms-1">System</span>' : '') + '</p>';
            threadBody.appendChild(div);
            threadBody.scrollTop = threadBody.scrollHeight;
        }
    });

    // ── Connection lifecycle ─────────────────────────────────────────────────

    connection.onreconnecting(() => showToastMsg('Board reconnecting\u2026', false));
    connection.onreconnected(() => showToastMsg('Board reconnected.', true));

    connection.start()
        .catch(err => console.warn('[CsLiveHelp] SignalR connection failed:', err));

    if (!hasAnyBoardColumn) return;

    // Stabilize repeated open/close for comment modals (prevents stuck overlay/loading state)
    document.addEventListener('hidden.bs.modal', function (e) {
        const modal = e.target;
        if (!modal?.id) return;

        const isCommentModal = modal.id.startsWith('commentModal-')
            || modal.id.startsWith('csCommentModal-')
            || modal.id.startsWith('intCommentModal-');

        if (!isCommentModal) return;

        // If no modal is currently open, force-clean any stale backdrop/body state.
        if (!document.querySelector('.modal.show')) {
            document.body.classList.remove('modal-open');
            document.body.style.removeProperty('padding-right');
            document.querySelectorAll('.modal-backdrop').forEach(function (b) { b.remove(); });
        }
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
