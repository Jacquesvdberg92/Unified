/**
 * dashboard-widgets.js
 * Uses event delegation — all listeners are on document so they work
 * immediately, survive widget reloads, and never need re-binding.
 *
 * Conventions:
 *   data-ajax-form              — form submits via fetch instead of navigating
 *   data-widget-key="k1 k2"    — space-separated widget keys to reload on success
 *   data-close-modal="modalId" — modal to hide on success
 *   data-modal-src="/path"     — button that loads a partial into data-bs-target modal
 *   data-bs-target="#modalId"  — target modal for data-modal-src buttons
 */
(function () {
    'use strict';

    // ── Toast ─────────────────────────────────────────────────────────────────
    function showToast(message, success) {
        let container = document.getElementById('dashboard-toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id        = 'dashboard-toast-container';
            container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            container.style.zIndex = '1100';
            document.body.appendChild(container);
        }
        const id  = 'toast-' + Date.now();
        const cls = success ? 'text-bg-success' : 'text-bg-danger';
        container.insertAdjacentHTML('beforeend', `
            <div id="${id}" class="toast align-items-center ${cls} border-0" role="alert" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">${message}</div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                </div>
            </div>`);
        const el = document.getElementById(id);
        const t  = new bootstrap.Toast(el, { delay: 3500 });
        t.show();
        el.addEventListener('hidden.bs.toast', () => el.remove());
    }

    // ── Widget reload ─────────────────────────────────────────────────────────
    async function reloadWidget(key) {
        const wrapper = document.querySelector(`[data-widget-key="${key}"]`);
        if (!wrapper) return;
        try {
            const res = await fetch(`/Home/GetWidget/${key}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (res.ok) wrapper.innerHTML = await res.text();
        } catch (_) { /* silent */ }
    }

    // ── Delegated: AJAX form submit ───────────────────────────────────────────
    document.addEventListener('submit', async function (e) {
        const form = e.target.closest('form[data-ajax-form]');
        if (!form) return;
        e.preventDefault();

        const widgetKeys   = (form.dataset.widgetKey || '').split(' ').map(s => s.trim()).filter(Boolean);
        const closeModalId = form.dataset.closeModal;

        let result = { success: false, message: 'Something went wrong.' };
        try {
            const res = await fetch(form.action, {
                method:  'POST',
                body:    new FormData(form),
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            result = { success: res.ok, message: res.ok ? 'Done.' : 'Something went wrong.' };
            try { Object.assign(result, await res.json()); } catch (_) { /* non-JSON */ }
        } catch (_) {
            result = { success: false, message: 'Request failed. Please try again.' };
        }

        showToast(result.message, result.success);

        if (result.success) {
            if (closeModalId) {
                const modalEl = document.getElementById(closeModalId);
                if (modalEl) bootstrap.Modal.getInstance(modalEl)?.hide();
            }
            for (const key of widgetKeys) await reloadWidget(key);
        }
    });

    // ── Delegated: modal partial loader ──────────────────────────────────────
    document.addEventListener('click', async function (e) {
        const trigger = e.target.closest('[data-modal-src]');
        if (!trigger) return;
        e.preventDefault();

        const src      = trigger.dataset.modalSrc;
        const targetId = (trigger.dataset.bsTarget || '').replace(/^#/, '');
        if (!src || !targetId) return;

        const modalEl = document.getElementById(targetId);
        if (!modalEl) return;

        const bodyEl = modalEl.querySelector('.modal-body');
        if (bodyEl) {
            bodyEl.innerHTML = `<div class="text-center py-4">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div></div>`;
        }

        // Open immediately — spinner shows while fetch runs
        bootstrap.Modal.getOrCreateInstance(modalEl).show();

        try {
            const res = await fetch(src, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            if (bodyEl) bodyEl.innerHTML = await res.text();
        } catch (err) {
            if (bodyEl) bodyEl.innerHTML = `<div class="alert alert-danger m-3">Failed to load form: ${err.message}</div>`;
        }
    });

    // ── Reset modal body when closed ──────────────────────────────────────────
    document.addEventListener('hidden.bs.modal', function (e) {
        const bodyEl = e.target.querySelector('.modal-body');
        if (bodyEl) {
            bodyEl.innerHTML = `<div class="text-center py-4">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div></div>`;
        }
    });

})();
