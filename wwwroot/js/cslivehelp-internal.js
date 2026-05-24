/**
 * cslivehelp-internal.js
 * Dedicated client for RequestsAllBrands.cshtml (internal CS page only).
 * 
 * Responsibilities:
 * - 4-column kanban (Open, InProgress, OnGoing, Completed)
 * - Internal comment threads + documents (pdf/doc/xlsx support)
 * - Drag-drop card movement with status updates
 * - Brand/ClientID filtering
 * - Team Leader escalation resolution
 * - SignalR live updates via window.csHub (set by shared cslivehelp.js)
 * 
 * This file MUST load AFTER cslivehelp.js and BEFORE page interaction.
 * It overrides shared SignalR handlers to provide internal-specific behavior.
 */

(function () {
    'use strict';

    // Wait for shared cslivehelp.js to establish connection and set window.csHub
    const connection = window.csHub;
    if (!connection) {
        console.error('[CsLiveHelp Internal] window.csHub not found. cslivehelp.js must load first.');
        return;
    }

    // Get antiforgery token injected by @Html.AntiForgeryToken() in the view
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    // ── Local helpers ────────────────────────────────────────────────────────

    function colEl(status) {
        return document.getElementById('col-' + status);
    }

    function cardEl(id) {
        return document.querySelector('[data-card-id="' + id + '"]');
    }

    function showToastMsg(msg, success) {
        if (typeof showToast === 'function') { showToast(msg, success); return; }
        console[success ? 'info' : 'warn']('[CsLiveHelp Internal]', msg);
    }

    function escHtml(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
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
        const col = colEl(status);
        const badge = document.getElementById('count-' + status);
        if (col && badge) badge.textContent = col.querySelectorAll('[data-card-id]').length;
    }

    const badgeClass = {
        Open: 'bg-secondary',
        InProgress: 'bg-warning text-dark',
        OnGoing: 'bg-warning text-dark',
        Escalated: 'bg-danger',
        Completed: 'bg-success'
    };

    // ── Internal-specific thread body detection ──────────────────────────────
    // Internal page only has intCommentModal-{id} modals.
    // DO NOT check offsetParent for visibility; the thread body may be inside
    // a hidden modal, but we still need to append comments to it.
    function findInternalThreadBodyElement(requestId) {
        const modal = document.getElementById('intCommentModal-' + requestId);
        if (!modal) return null;

        // Pattern 1: Look for existing .thread-body div (scrollable container)
        let threadBody = modal.querySelector('.thread-body');
        if (threadBody) return threadBody;

        // Pattern 2: Look for .border.rounded div (fallback for internal)
        threadBody = modal.querySelector('.modal-body .border.rounded');
        if (threadBody) return threadBody;

        // Pattern 3: If no thread body exists, we'll create one in the modal-body
        // This handles the case where the modal is empty or has no comments initially
        const modalBody = modal.querySelector('.modal-body');
        if (modalBody) {
            // Check if the modal body has a textarea (comment form)
            const textarea = modalBody.querySelector('textarea');
            if (textarea) {
                // Create a thread-body div before the textarea
                const threadBodyDiv = document.createElement('div');
                threadBodyDiv.className = 'thread-body mb-3 border rounded p-2 small';
                threadBodyDiv.style.cssText = 'max-height:200px;overflow-y:auto';
                threadBodyDiv.id = 'threadBody-' + requestId;

                // Insert before the first non-div element (the textarea or its wrapper)
                modalBody.insertBefore(threadBodyDiv, textarea.closest('.form-control') || textarea);
                return threadBodyDiv;
            }
        }

        return null;
    }

    // ── Override CardStatusChanged to handle internal board (4 columns) ───────
    // The shared handler maps Escalated → InProgress on AM page,
    // but internal page has all 4 columns: Open, InProgress, OnGoing, Completed.
    // We need to override just for this page.

    // Remove the shared CardStatusChanged listener and register our own
    // (This is possible because we hook into the same connection object)
    connection.off('CardStatusChanged');
    connection.on('CardStatusChanged', function (data) {
        const card = cardEl(data.id);
        if (!card) return;

        const newStatus = data.newStatus;
        const targetCol = colEl(newStatus);
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
            badge.textContent = newStatus;
        }

        if (data.assignedTo) {
            const assignedEl = card.querySelector('.cs-assigned-name');
            if (assignedEl) {
                assignedEl.textContent = data.assignedTo;
                const row = card.querySelector('.cs-assigned-row');
                if (row) row.style.display = '';
            }
        }

        console.log('[CsLiveHelp Internal] Card status changed:', data.id, '→', newStatus);
    });

    // ── Override CommentAdded to use internal modal detection ──────────────────
    connection.off('CommentAdded');
    connection.on('CommentAdded', function (data) {
        const requestId = data.requestId;

        // Update card comment count badge (internal uses .int-comment-count)
        const card = cardEl(requestId);
        if (card) {
            let countBadge = card.querySelector('.int-comment-count');

            if (countBadge) {
                // Badge exists: increment it
                const n = parseInt(countBadge.textContent, 10) || 0;
                countBadge.textContent = n + 1;
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
                        buttonDiv.innerHTML = '<button class="btn btn-link btn-sm p-0 text-muted" style="font-size:.72rem" data-bs-toggle="modal" data-bs-target="#intCommentModal-' + requestId + '" title="View thread"><i class="bx bx-comment-detail me-1"></i><span class="int-comment-count">1</span> comment(s) — View thread</button>';

                        insertAfter.parentElement.insertBefore(buttonDiv, insertAfter);
                        console.log('[CsLiveHelp Internal] Created comment count button for first comment on request', requestId);
                    }
                }
            }
        }

        // Find internal thread body; create it if it doesn't exist yet
        let threadBody = findInternalThreadBodyElement(requestId);

        if (!threadBody) {
            console.warn('[CsLiveHelp Internal] CommentAdded: could not find or create thread body for request', requestId);
            return;
        }

        // If threadBody is a .modal-body (no thread container yet), create one
        if (threadBody.classList.contains('modal-body')) {
            const wrap = document.createElement('div');
            wrap.className = 'thread-body mb-3 border rounded p-2 small';
            wrap.style.cssText = 'max-height:200px;overflow-y:auto';
            wrap.id = 'threadBody-' + requestId;

            // Insert before the textarea or form elements
            const textarea = threadBody.querySelector('textarea');
            const textarea_wrapper = textarea?.closest('[class*="mt-"]') || textarea?.parentElement;
            if (textarea_wrapper) {
                threadBody.insertBefore(wrap, textarea_wrapper);
            } else {
                threadBody.insertBefore(wrap, threadBody.firstChild);
            }
            threadBody = wrap;
        }

        // Remove "No comments yet" placeholder if present
        const emptyP = threadBody.querySelector('p.text-muted');
        if (emptyP) emptyP.remove();

        // Build comment HTML
        const dt = new Date(data.createdAt);
        const ts = isNaN(dt.getTime()) ? '(timestamp unavailable)' : dt.toLocaleString();
        const div = document.createElement('div');
        div.className = 'mb-1 ' + (data.isSystem ? 'text-muted fst-italic' : '');
        div.innerHTML =
            '<span class="fw-semibold">' + escHtml(ts) + ':</span> ' +
            escHtml(data.body);

        // Add image/document if provided
        if (data.imagePath) {
            const ext = data.imagePath.split('.').pop().toLowerCase();
            const isImg = ['jpg', 'jpeg', 'png', 'gif', 'webp'].includes(ext);

            if (isImg) {
                div.innerHTML +=
                    '<div class="mt-1"><a href="' + escHtml(data.imagePath) + '" target="_blank" rel="noopener">' +
                    '<img src="' + escHtml(data.imagePath) + '" alt="Attachment" class="img-fluid rounded" style="max-height:160px" />' +
                    '</a></div>';
            } else {
                div.innerHTML +=
                    '<div class="mt-1"><a href="' + escHtml(data.imagePath) + '" target="_blank" rel="noopener" class="btn btn-outline-secondary btn-sm py-0 px-2">' +
                    '<i class="bx bx-file me-1"></i>' + escHtml(data.imagePath.split('/').pop()) +
                    '</a></div>';
            }
        }

        threadBody.appendChild(div);
        threadBody.scrollTop = threadBody.scrollHeight;

        console.log('[CsLiveHelp Internal] Comment added to request', requestId, '— author:', data.author);
    });

    // ── Modal cleanup and form wiring ────────────────────────────────────────

    function ajaxFormSetup(formEl, modalEl) {
        if (formEl.dataset.ajaxWired) return;
        formEl.dataset.ajaxWired = '1';
        formEl.dataset.isSubmitting = '0';

        formEl.addEventListener('submit', async function (e) {
            e.preventDefault();

            if (formEl.dataset.isSubmitting === '1') {
                console.warn('[CsLiveHelp Internal] Form submission already in progress');
                return;
            }

            formEl.dataset.isSubmitting = '1';

            const fd = new FormData(formEl);
            const url = formEl.action || formEl.getAttribute('action');
            const btn = formEl.querySelector('[type="submit"]');
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
                    if (modalEl) {
                        const bsModal = bootstrap.Modal.getInstance(modalEl);
                        if (bsModal) bsModal.hide();
                    }
                    formEl.reset();
                    showToastMsg(data.message ?? 'Done.', true);
                    console.log('[CsLiveHelp Internal] Form submitted successfully:', url);
                } else {
                    showToastMsg(data.error ?? 'An error occurred. Please try again.', false);
                    console.warn('[CsLiveHelp Internal] Form submission error:', data);
                }
            } catch (err) {
                showToastMsg('Network error. Please try again.', false);
                console.error('[CsLiveHelp Internal] Form submission network error:', err);
            } finally {
                formEl.dataset.isSubmitting = '0';
                if (btn) {
                    btn.disabled = false;
                    if (originalText) btn.innerHTML = originalText;
                }
            }
        });
    }

    // Wire internal comment modals on page load
    function wireInternalCommentModals() {
        document.querySelectorAll('[id^="intCommentModal-"]').forEach(function (modal) {
            const form = modal.querySelector('form');
            if (form) ajaxFormSetup(form, modal);
        });
    }

    wireInternalCommentModals();

    // Modal state cleanup on show/hidden
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

    document.addEventListener('show.bs.modal', function (e) {
        const modal = e.target;
        if (!modal?.id) return;
        if (!modal.id.startsWith('intCommentModal-')) return;
        cleanupStaleModalState();
    });

    // Use 'hide.bs.modal' (fires BEFORE aria-hidden applied) not 'hidden.bs.modal' (fires AFTER)
    // This ensures we clear focus and prevent accessibility violations
    document.addEventListener('hide.bs.modal', function (e) {
        const modal = e.target;
        if (!modal?.id) return;
        if (!modal.id.startsWith('intCommentModal-')) return;

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
        if (!modal.id.startsWith('intCommentModal-')) return;

        cleanupStaleModalState();
    });

    console.log('[CsLiveHelp Internal] Initialized internal page SignalR handlers');

    // ── FILTER ───────────────────────────────────────────────────────────────

    const brandSel = document.getElementById('allBrandsBrandFilter');
    const clientInput = document.getElementById('allBrandsClientIdFilter');
    const applyBtn = document.getElementById('allBrandsFilterApply');
    const clearBtn = document.getElementById('allBrandsFilterClear');

    function applyFilter() {
        const brandId = brandSel?.value ?? '';
        const query = (clientInput?.value ?? '').toLowerCase().trim();
        document.querySelectorAll('.kanban-col [data-card-id]').forEach(function (card) {
            const matchBrand = !brandId || String(card.dataset.brandId) === brandId;
            const matchClient = !query || (card.dataset.clientId ?? '').includes(query);
            card.style.display = (matchBrand && matchClient) ? '' : 'none';
        });
    }

    applyBtn?.addEventListener('click', applyFilter);
    clientInput?.addEventListener('keydown', function (e) { if (e.key === 'Enter') applyFilter(); });
    clearBtn?.addEventListener('click', function () {
        if (brandSel) brandSel.value = '';
        if (clientInput) clientInput.value = '';
        document.querySelectorAll('.kanban-col [data-card-id]').forEach(function (card) {
            card.style.display = '';
        });
    });

    // ── DRAG-DROP INITIALIZATION ─────────────────────────────────────────────

    // Only load SortableJS if it is available
    if (typeof Sortable === 'undefined') {
        console.warn('[CsLiveHelp Internal] Sortable.js not loaded; drag-drop disabled.');
        return;
    }

    document.querySelectorAll('.kanban-col').forEach(function (col) {
        Sortable.create(col, {
            group: 'int-kanban',
            animation: 150,
            ghostClass: 'sortable-ghost',
            dragClass: 'sortable-drag',
            filter: 'button, a, input, textarea',
            preventOnFilter: false,
            onEnd: async function (evt) {
                const card = evt.item;
                const cardId = card.dataset.cardId;
                const newStatus = evt.to.dataset.status;
                const oldStatus = evt.from.dataset.status;

                if (!cardId || !newStatus || newStatus === oldStatus) return;

                // Remove empty-column hint from target
                removeEmptyHint(evt.to);

                try {
                    const fd = new FormData();
                    fd.append('status', newStatus);
                    fd.append('__RequestVerificationToken', token);

                    const res = await fetch(`/CsLiveHelp/InternalUpdateStatusJson/${cardId}`, {
                        method: 'POST',
                        body: fd,
                        headers: { 'X-Requested-With': 'XMLHttpRequest' }
                    });

                    if (res.ok) {
                        const badge = card.querySelector('.badge');
                        if (badge && badgeClass[newStatus]) {
                            badge.className = 'badge ' + badgeClass[newStatus];
                            badge.textContent = newStatus;
                        }
                        addEmptyHintIfEmpty(evt.from);
                        updateColCount(newStatus);
                        updateColCount(oldStatus);
                        showToastMsg('Card moved to ' + newStatus, true);
                    } else {
                        // Revert on failure
                        evt.from.insertBefore(card, evt.from.children[evt.oldIndex] ?? null);
                        showToastMsg('Failed to update card status.', false);
                    }
                } catch (_) {
                    // Revert on error
                    evt.from.insertBefore(card, evt.from.children[evt.oldIndex] ?? null);
                    showToastMsg('Network error — card move reverted.', false);
                }
            }
        });
    });

    // Highlight drop target while dragging
    document.addEventListener('dragover', function (e) {
        const col = e.target.closest('.kanban-col');
        document.querySelectorAll('.kanban-col').forEach(c => c.classList.remove('drag-over'));
        if (col) col.classList.add('drag-over');
    });

    document.addEventListener('dragleave', function (e) {
        if (!e.relatedTarget?.closest('.kanban-col'))
            document.querySelectorAll('.kanban-col').forEach(c => c.classList.remove('drag-over'));
    });

    document.addEventListener('drop', function () {
        document.querySelectorAll('.kanban-col').forEach(c => c.classList.remove('drag-over'));
    });
})();
