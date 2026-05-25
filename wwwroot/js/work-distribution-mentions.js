(function () {
    'use strict';

    function buildSuggestionsEl() {
        const wrap = document.createElement('div');
        wrap.className = 'wd-mention-suggestions border rounded bg-white shadow-sm d-none';
        wrap.style.position = 'fixed';
        wrap.style.zIndex = '1080';
        wrap.style.maxHeight = '220px';
        wrap.style.overflowY = 'auto';
        wrap.style.minWidth = '240px';
        wrap.style.maxWidth = '420px';
        document.body.appendChild(wrap);
        return wrap;
    }

    function getCaretCoordinates(textarea, position) {
        const div = document.createElement('div');
        const style = window.getComputedStyle(textarea);
        const rect = textarea.getBoundingClientRect();

        const properties = [
            'boxSizing', 'width', 'height', 'overflowX', 'overflowY',
            'borderTopWidth', 'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth',
            'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft',
            'fontStyle', 'fontVariant', 'fontWeight', 'fontStretch', 'fontSize',
            'fontSizeAdjust', 'lineHeight', 'fontFamily', 'textAlign', 'textTransform',
            'textIndent', 'textDecoration', 'letterSpacing', 'wordSpacing', 'tabSize', 'whiteSpace'
        ];

        div.style.position = 'fixed';
        div.style.left = '-9999px';
        div.style.top = '0';
        div.style.whiteSpace = 'pre-wrap';
        div.style.wordWrap = 'break-word';

        properties.forEach(function (prop) {
            div.style[prop] = style[prop];
        });

        div.textContent = (textarea.value || '').substring(0, position);

        const span = document.createElement('span');
        span.textContent = (textarea.value || '').substring(position) || '.';
        div.appendChild(span);
        document.body.appendChild(div);

        const top = rect.top + span.offsetTop - textarea.scrollTop;
        const left = rect.left + span.offsetLeft - textarea.scrollLeft;

        document.body.removeChild(div);

        return { top, left };
    }

    function positionSuggestions(state) {
        const textarea = state.textarea;
        const suggestionsEl = state.suggestionsEl;
        if (!textarea || !suggestionsEl || suggestionsEl.classList.contains('d-none')) return;

        const caretPos = textarea.selectionStart || 0;
        const caret = getCaretCoordinates(textarea, caretPos);
        const taRect = textarea.getBoundingClientRect();

        let top = caret.top + 24;
        let left = caret.left;

        const viewportPadding = 8;
        const maxLeft = window.innerWidth - suggestionsEl.offsetWidth - viewportPadding;
        if (left > maxLeft) left = Math.max(viewportPadding, maxLeft);

        const maxTop = window.innerHeight - suggestionsEl.offsetHeight - viewportPadding;
        if (top > maxTop) {
            top = Math.max(viewportPadding, taRect.top - suggestionsEl.offsetHeight - 6);
        }

        suggestionsEl.style.top = top + 'px';
        suggestionsEl.style.left = left + 'px';
    }

    function hideSuggestions(state) {
        if (!state?.suggestionsEl) return;
        state.suggestionsEl.classList.add('d-none');
        state.suggestionsEl.innerHTML = '';
    }

    function parseMentionQuery(value, caretPos) {
        const before = value.slice(0, caretPos);
        const match = before.match(/(^|\s)@([A-Za-z0-9._\- ]{0,48})$/);
        if (!match) return null;

        const query = (match[2] || '').trimStart();
        const start = before.lastIndexOf('@');
        if (start < 0) return null;

        return { query, start, end: caretPos };
    }

    async function fetchCandidates(url) {
        if (!url) return [];

        try {
            const res = await fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!res.ok) return [];

            const data = await res.json();
            if (!data?.success || !Array.isArray(data.candidates)) return [];

            return data.candidates
                .filter(name => typeof name === 'string' && name.trim().length > 0)
                .map(name => name.trim());
        } catch {
            return [];
        }
    }

    function renderSuggestions(state) {
        const { suggestionsEl, matches, selectedIndex } = state;
        if (!suggestionsEl) return;

        if (!matches.length) {
            hideSuggestions(state);
            return;
        }

        suggestionsEl.classList.remove('d-none');
        suggestionsEl.innerHTML = matches.map((name, idx) => `
            <button type="button" class="dropdown-item text-start ${idx === selectedIndex ? 'active' : ''}" data-mention-name="${name}">
                @${name}
            </button>
        `).join('');

        const activeItem = suggestionsEl.querySelector('.dropdown-item.active');
        if (activeItem) {
            activeItem.scrollIntoView({ block: 'nearest' });
        }

        positionSuggestions(state);
    }

    function applyMention(textarea, state, chosenName) {
        if (!state.currentRange) return;

        const { start, end } = state.currentRange;
        const value = textarea.value || '';
        const replacement = `@${chosenName} `;

        textarea.value = value.slice(0, start) + replacement + value.slice(end);
        const nextPos = start + replacement.length;
        textarea.setSelectionRange(nextPos, nextPos);
        textarea.focus();

        state.currentRange = null;
        state.matches = [];
        state.selectedIndex = 0;
        renderSuggestions(state);
    }

    async function wireTextarea(textarea) {
        if (!textarea || textarea.dataset.wdMentionsWired === '1') return;
        textarea.dataset.wdMentionsWired = '1';

        const suggestionsEl = buildSuggestionsEl();
        const candidates = await fetchCandidates(textarea.dataset.mentionsUrl || '');

        const state = {
            textarea,
            candidates,
            matches: [],
            selectedIndex: 0,
            currentRange: null,
            suggestionsEl
        };

        textarea.addEventListener('input', function () {
            const range = parseMentionQuery(textarea.value || '', textarea.selectionStart || 0);
            state.currentRange = range;

            if (!range || !state.candidates.length) {
                state.matches = [];
                state.selectedIndex = 0;
                renderSuggestions(state);
                return;
            }

            const q = (range.query || '').toLowerCase();
            state.matches = state.candidates
                .filter(name => !q || name.toLowerCase().split(/\s+/).some(word => word.startsWith(q)));
            state.selectedIndex = 0;
            renderSuggestions(state);
        });

        textarea.addEventListener('keydown', function (e) {
            if (!state.matches.length) return;

            if (e.key === 'ArrowDown') {
                e.preventDefault();
                state.selectedIndex = (state.selectedIndex + 1) % state.matches.length;
                renderSuggestions(state);
                return;
            }

            if (e.key === 'ArrowUp') {
                e.preventDefault();
                state.selectedIndex = (state.selectedIndex - 1 + state.matches.length) % state.matches.length;
                renderSuggestions(state);
                return;
            }

            if (e.key === 'Tab' || (e.key === 'Enter' && !e.shiftKey)) {
                e.preventDefault();
                const chosen = state.matches[state.selectedIndex] || state.matches[0];
                if (chosen) applyMention(textarea, state, chosen);
                return;
            }

            if (e.key === 'Escape') {
                state.matches = [];
                state.selectedIndex = 0;
                state.currentRange = null;
                renderSuggestions(state);
            }
        });

        suggestionsEl?.addEventListener('click', function (e) {
            const btn = e.target.closest('[data-mention-name]');
            if (!btn) return;
            const name = btn.getAttribute('data-mention-name') || '';
            if (!name) return;
            applyMention(textarea, state, name);
        });

        textarea.addEventListener('scroll', function () {
            positionSuggestions(state);
        });

        textarea.addEventListener('click', function () {
            positionSuggestions(state);
        });

        window.addEventListener('resize', function () {
            positionSuggestions(state);
        });

        textarea.addEventListener('blur', function () {
            setTimeout(function () {
                state.matches = [];
                state.selectedIndex = 0;
                hideSuggestions(state);
            }, 120);
        });
    }

    function wireAll() {
        document.querySelectorAll('textarea[data-mentions-enabled="1"]').forEach(wireTextarea);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', wireAll);
    } else {
        wireAll();
    }
})();
