(function () {
    'use strict';

    function encodeHtml(text) {
        return (text || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    function highlightMentions(text) {
        const encoded = encodeHtml(text);
        return encoded.replace(/(^|\s)(@[A-Za-z][A-Za-z0-9._\-]{1,48})/g, '$1<span class="fw-semibold text-primary">$2</span>');
    }

    function wireBodies() {
        document.querySelectorAll('[data-wd-body="1"]').forEach(function (el) {
            if (el.dataset.wdHighlighted === '1') return;
            el.dataset.wdHighlighted = '1';
            el.innerHTML = highlightMentions(el.textContent || '');
        });
    }

    window.WorkDistributionView = window.WorkDistributionView || {};
    window.WorkDistributionView.highlightMentions = wireBodies;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', wireBodies);
    } else {
        wireBodies();
    }

    window.addEventListener('wd:content-updated', wireBodies);
})();
