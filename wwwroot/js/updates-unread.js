// Unread mark for the Updates Feed.
//
// The sidebar badge carries the ids of the newest updates (data-update-ids). This browser
// remembers the highest id it has seen on the feed, so the badge counts the newer ones and
// cards/widget rows with a newer id get a "New" label. Opening the feed marks them all seen.
// live-notifications.js calls add() when an update is posted, so the badge updates without a reload.
(function () {
    'use strict';

    var STORAGE_KEY = 'updates:lastSeenId';
    var badge = document.getElementById('sidebarUpdatesUnread');
    if (!badge) return;

    var ids = (badge.dataset.updateIds || '')
        .split(',')
        .map(function (v) { return parseInt(v, 10); })
        .filter(function (v) { return v > 0; });

    function maxId(list) {
        return list.reduce(function (m, v) { return v > m ? v : m; }, 0);
    }

    function readLastSeen() {
        try {
            var v = parseInt(localStorage.getItem(STORAGE_KEY) || '', 10);
            return isNaN(v) ? null : v;
        } catch (_) { return null; }
    }

    function writeLastSeen(id) {
        try { localStorage.setItem(STORAGE_KEY, String(id)); } catch (_) { }
    }

    var lastSeen = readLastSeen();
    if (lastSeen === null) {
        // First visit in this browser: start from what's there now rather than flagging every update
        lastSeen = maxId(ids);
        writeLastSeen(lastSeen);
    }

    // What "new" means for labels on this page; kept steady so in-place filter swaps still show them
    var baseline = lastSeen;
    var path = (window.location.pathname || '').toLowerCase().replace(/\/+$/, '');
    var onFeed = path === '/updates' || path === '/updates/feed';

    function renderBadge() {
        var count = ids.filter(function (id) { return id > lastSeen; }).length;
        badge.textContent = count > 9 ? '9+' : String(count);
        badge.classList.toggle('d-none', count === 0);
        badge.title = count === 1 ? '1 new update' : count + ' new updates';
    }

    function markNew(root) {
        (root || document).querySelectorAll('[data-update-id]').forEach(function (el) {
            var label = el.querySelector('[data-update-new]');
            if (!label) return;
            label.classList.toggle('d-none', !(parseInt(el.dataset.updateId, 10) > baseline));
        });
    }

    function markAllSeen() {
        var newest = maxId(ids);
        if (newest > lastSeen) {
            lastSeen = newest;
            writeLastSeen(lastSeen);
        }
        renderBadge();
    }

    function init() {
        markNew(document);
        if (onFeed) markAllSeen();
        else renderBadge();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    document.addEventListener('inplace:load', function (e) { markNew(e.detail && e.detail.region); });

    // Another tab opened the feed: clear the badge here too
    window.addEventListener('storage', function (e) {
        if (e.key !== STORAGE_KEY) return;
        var v = parseInt(e.newValue || '', 10);
        if (!isNaN(v) && v > lastSeen) {
            lastSeen = v;
            renderBadge();
        }
    });

    window.UnifiedUpdatesUnread = {
        add: function (id) {
            id = parseInt(id, 10);
            if (!(id > 0) || ids.indexOf(id) !== -1) return;
            ids.unshift(id);
            renderBadge();
        }
    };
})();
