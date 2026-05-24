(function () {
    'use strict';

    const activeMentionState = new Map();

    function buildSuggestionsEl(textarea) {
        const wrap = document.createElement('div');
        wrap.className = 'cslh-mention-suggestions border rounded bg-white shadow-sm d-none';
        wrap.style.position = 'absolute';
        wrap.style.zIndex = '1080';
        wrap.style.maxHeight = '180px';
        wrap.style.overflowY = 'auto';
        wrap.style.minWidth = '220px';

        const parent = textarea.parentElement;
        if (!parent) return null;
        parent.style.position = parent.style.position || 'relative';
        parent.appendChild(wrap);
        return wrap;
    }

    async function fetchCandidates(requestId) {
        if (!requestId) return [];

        try {
            const res = await fetch(`/CsLiveHelp/MentionCandidates/${requestId}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!res.ok) return [];
            const data = await res.json();
            if (!data?.success || !Array.isArray(data.candidates)) return [];
            return data.candidates;
        } catch {
            return [];
        }
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

    function renderSuggestions(state) {
        const { suggestionsEl, matches, selectedIndex } = state;
        if (!suggestionsEl) return;

        if (!matches.length) {
            suggestionsEl.classList.add('d-none');
            suggestionsEl.innerHTML = '';
            return;
        }

        suggestionsEl.classList.remove('d-none');
        suggestionsEl.innerHTML = matches.map((name, idx) => `
            <button type="button" class="dropdown-item text-start ${idx === selectedIndex ? 'active' : ''}" data-mention-name="${name}">
                @${name}
            </button>
        `).join('');
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

    function wireTextarea(textarea) {
        if (!textarea || textarea.dataset.cslhMentionsWired === '1') return;
        textarea.dataset.cslhMentionsWired = '1';

        const form = textarea.closest('form');
        const hiddenId = form?.querySelector('input[name="id"]')?.value || '';
        const action = form?.getAttribute('action') || '';
        const actionId = action ? ((action.match(/\/(\d+)(?:\?|$)/)?.[1]) || '') : '';
        const requestId = hiddenId || actionId;
        const suggestionsEl = buildSuggestionsEl(textarea);

        const state = {
            candidates: [],
            matches: [],
            selectedIndex: 0,
            currentRange: null,
            suggestionsEl,
            requestId
        };
        activeMentionState.set(textarea, state);

        textarea.addEventListener('focus', async function () {
            if (state.candidates.length) return;
            state.candidates = await fetchCandidates(state.requestId);
        });

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
                .filter(name => !q || name.toLowerCase().includes(q))
                .slice(0, 8);
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

        textarea.addEventListener('blur', function () {
            setTimeout(function () {
                state.matches = [];
                state.selectedIndex = 0;
                renderSuggestions(state);
            }, 120);
        });
    }

    function wireAll() {
        document.querySelectorAll('textarea[name="body"]').forEach(wireTextarea);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', wireAll);
    } else {
        wireAll();
    }

    document.addEventListener('shown.bs.modal', function () {
        wireAll();
    });
})();
