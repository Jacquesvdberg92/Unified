// In-place updates: submit opted-in forms and follow opted-in links in the background,
// then swap only the changed regions of the page instead of reloading the whole screen.
//
// Markup:
//   data-inplace-region="name"   a part of the page that can be replaced. The layout's main
//                                content is the region "page".
//   data-inplace                 on a form or link, or on any element around them, opts them in.
//                                Give region names ("results" or "list,team") to swap those;
//                                with no value the nearest region around the form or link is
//                                swapped. data-inplace="off" opts a form or link back out.
//                                Links only update in place when they stay on the same page;
//                                links to other pages still navigate normally.
//
// The server needs no changes: requests carry X-Inplace: 1, _ViewStart then renders the view
// without the layout, and post-redirect-get still works because fetch follows the redirect.
// Page scripts that bind to elements inside a region can re-bind on the "inplace:load" event.
(function () {
    'use strict';

    var TOAST_STYLES = {
        success: { duration: 4000, style: { background: '#28a745' } },
        warning: { duration: 5000, style: { background: '#ffc107', color: '#212529' } },
        error:   { duration: 6000, style: { background: '#dc3545' } }
    };

    function showToast(kind, text) {
        if (!text || typeof Toastify === 'undefined') return;
        var opts = TOAST_STYLES[kind] || TOAST_STYLES.success;
        Toastify({ text: text, duration: opts.duration, gravity: 'top', position: 'right', style: opts.style, stopOnFocus: true }).showToast();
    }

    // Shows the TempData messages that _Toast.cshtml writes into a JSON block
    function showToastsFrom(root) {
        var el = root.querySelector('#tempDataToasts');
        if (!el) return;
        try {
            var data = JSON.parse(el.textContent);
            ['success', 'warning', 'error'].forEach(function (kind) { showToast(kind, data[kind]); });
        } catch (_) { }
    }

    function optIn(el) {
        var host = el.closest('[data-inplace]');
        if (!host) return null;
        var own = el.getAttribute('data-inplace');
        var value = own !== null ? own : host.getAttribute('data-inplace');
        if (value === 'off') return null;
        if (value) return value.split(',').map(function (s) { return s.trim(); }).filter(Boolean);
        var region = el.closest('[data-inplace-region]');
        return [region ? region.getAttribute('data-inplace-region') : 'page'];
    }

    // Same controller and action: "/Withdrawals", "/Withdrawals/Index" and "/Withdrawals/Index/5"
    // are all the same page
    function samePage(url) {
        var norm = function (p) {
            var parts = p.toLowerCase().split('/').filter(Boolean);
            return (parts[0] || 'home') + '/' + (parts[1] || 'index');
        };
        return url.origin === location.origin && norm(url.pathname) === norm(location.pathname);
    }

    function regionEl(root, name) {
        return root.querySelector('[data-inplace-region="' + CSS.escape(name) + '"]');
    }

    function setBusy(names, busy) {
        names.forEach(function (name) {
            var el = regionEl(document, name);
            if (!el) return;
            el.setAttribute('aria-busy', busy ? 'true' : 'false');
            el.classList.toggle('inplace-busy', busy);
        });
    }

    // Closes Bootstrap modals that are open in, or about to be removed with, a swapped region
    function closeModals(names, form) {
        var modals = [];
        var formModal = form && form.closest('.modal.show');
        if (formModal) modals.push(formModal);
        names.forEach(function (name) {
            var el = regionEl(document, name);
            if (el) el.querySelectorAll('.modal.show').forEach(function (m) { modals.push(m); });
        });
        if (!modals.length) return;
        modals.forEach(function (m) {
            if (window.bootstrap && bootstrap.Modal.getInstance(m)) bootstrap.Modal.getInstance(m).hide();
        });
        // A modal removed while open leaves its backdrop behind, so clear it here
        document.querySelectorAll('.modal-backdrop').forEach(function (b) { b.remove(); });
        document.body.classList.remove('modal-open');
        document.body.style.removeProperty('overflow');
        document.body.style.removeProperty('padding-right');
        if (formModal) formModal.querySelectorAll('form').forEach(function (f) { f.reset(); });
    }

    function swap(doc, names, form) {
        var pairs = [];
        for (var i = 0; i < names.length; i++) {
            var current = regionEl(document, names[i]);
            var next = regionEl(doc, names[i]);
            if (!current || !next) return false;
            pairs.push([current, next]);
        }

        var focusedId = document.activeElement && document.activeElement.id;
        closeModals(names, form);

        pairs.forEach(function (p) {
            var next = document.importNode(p[1], true);
            p[0].replaceWith(next);
            if (window.bootstrap) {
                next.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (t) { new bootstrap.Tooltip(t); });
            }
            document.dispatchEvent(new CustomEvent('inplace:load', { detail: { region: next, name: p[0].getAttribute('data-inplace-region') } }));
        });

        if (focusedId) {
            var again = document.getElementById(focusedId);
            if (again && again !== document.activeElement) again.focus({ preventScroll: true });
        }
        var title = doc.querySelector('title');
        if (title && title.textContent.trim()) document.title = title.textContent;
        showToastsFrom(doc);
        return true;
    }

    var inFlight = null;

    function load(url, init, names, opts) {
        if (inFlight) inFlight.abort();
        var controller = new AbortController();
        inFlight = controller;

        init.headers = { 'X-Inplace': '1' };
        init.credentials = 'same-origin';
        init.cache = 'no-store';   // never let a layout-less response stand in for a full page
        init.signal = controller.signal;

        setBusy(names, true);
        if (opts.button) opts.button.disabled = true;

        return fetch(url, init).then(function (res) {
            return res.text().then(function (html) {
                if (!res.ok) {
                    showToast('error', 'That did not work (' + res.status + '). Please try again.');
                    return;
                }
                // A post that lands on a different page is a navigation: load that page normally
                // so its own scripts run (the post itself is done, so this only repeats the GET)
                if (res.redirected && !samePage(new URL(res.url))) {
                    location.assign(res.url);
                    return;
                }
                var doc = new DOMParser().parseFromString(html, 'text/html');
                if (!swap(doc, names, opts.form)) {
                    // Somewhere else entirely (for example the sign-in page): load it normally
                    if (res.redirected || init.method === 'GET') location.assign(res.url);
                    else location.reload();
                    return;
                }
                var finalUrl = res.url || url;
                if (opts.push && finalUrl !== location.href) {
                    // Mark the entry we leave too, so Back swaps the same regions back
                    if (!(history.state && history.state.inplace)) history.replaceState({ inplace: names }, '');
                    history.pushState({ inplace: names }, '', finalUrl);
                }
                else if (!opts.push && res.redirected) history.replaceState({ inplace: names }, '', finalUrl);
            });
        }).catch(function (err) {
            if (err && err.name === 'AbortError') return;
            showToast('error', 'Could not reach the server. Check your connection and try again.');
        }).finally(function () {
            if (inFlight === controller) inFlight = null;
            setBusy(names, false);
            if (opts.button && opts.button.isConnected) opts.button.disabled = false;
        });
    }

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (e.defaultPrevented || !(form instanceof HTMLFormElement)) return;
        var names = optIn(form);
        if (!names) return;
        if (form.target && form.target !== '_self') return;

        var submitter = e.submitter;
        var method = ((submitter && submitter.getAttribute('formmethod')) || form.method || 'get').toUpperCase();
        var action = (submitter && submitter.getAttribute('formaction')) || form.action || location.href;
        var data = new FormData(form);
        if (submitter && submitter.name) data.append(submitter.name, submitter.value);

        if (method === 'GET') {
            var url = new URL(action, location.href);
            if (!samePage(url)) return;
            url.search = new URLSearchParams(data).toString();
            e.preventDefault();
            load(url.toString(), { method: 'GET' }, names, { push: true, form: form, button: submitter });
        } else {
            e.preventDefault();
            load(action, { method: 'POST', body: data }, names, { push: false, form: form, button: submitter });
        }
    });

    document.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        var link = e.target.closest && e.target.closest('a[href]');
        if (!link || link.target || link.hasAttribute('download')) return;
        var names = optIn(link);
        if (!names) return;
        var url = new URL(link.href, location.href);
        if (!samePage(url) || (url.hash && url.search === location.search)) return;

        e.preventDefault();
        load(url.toString(), { method: 'GET' }, names, { push: true });
    });

    // Back and forward between in-place states reload just the regions again
    window.addEventListener('popstate', function (e) {
        if (e.state && e.state.inplace) load(location.href, { method: 'GET' }, e.state.inplace, { push: false });
    });

    window.unifiedInplace = { showToast: showToast, showToastsFrom: showToastsFrom };
})();
