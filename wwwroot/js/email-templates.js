// EmailTemplates: shared utilities

// Copy clipboard helper
function etFlashBtn(btn, label, orig) {
    btn.innerHTML = '<i class="ri-check-line me-1"></i>' + label;
    setTimeout(function () { btn.innerHTML = orig; }, 2500);
}

// BrandManager / Brands: copy signature HTML
document.querySelectorAll('.copy-sig-btn').forEach(function (btn) {
    btn.addEventListener('click', function () {
        var sig = JSON.parse(this.dataset.sig);
        var self = this;
        navigator.clipboard.writeText(sig).then(function () {
            self.textContent = '\u2714 Copied!';
            setTimeout(function () { self.innerHTML = '<i class="ri-clipboard-line me-1"></i> Copy Signature HTML'; }, 2500);
        });
    });
});

// Brands: copy email address buttons
document.querySelectorAll('.copy-btn').forEach(function (btn) {
    btn.addEventListener('click', function () {
        var text = this.dataset.copy;
        var self = this;
        navigator.clipboard.writeText(text).then(function () {
            self.innerHTML = '<i class="bx bx-check"></i>';
            setTimeout(function () { self.innerHTML = '<i class="bx bx-copy-alt"></i>'; }, 2000);
        });
    });
});

// Brands: search bar
(function () {
    var searchInput = document.getElementById('brandSearch');
    if (!searchInput) return;
    var allCards = document.querySelectorAll('.brand-card');
    searchInput.addEventListener('input', function () {
        var term = this.value.toLowerCase().trim();
        allCards.forEach(function (card) {
            card.style.display = (!term || card.dataset.brandName.includes(term)) ? '' : 'none';
        });
    });
})();

// Brands: jump-to dropdown
(function () {
    var brandJump = document.getElementById('brandJump');
    if (!brandJump) return;
    brandJump.addEventListener('change', function () {
        var id = this.value;
        if (!id) return;
        var target = document.getElementById(id);
        if (target) {
            target.scrollIntoView({ behavior: 'smooth', block: 'start' });
            target.classList.add('border', 'border-primary');
            setTimeout(function () { target.classList.remove('border', 'border-primary'); }, 2000);
        }
        this.value = '';
    });
})();

// Create/Edit: Quill body editor + token insert + live preview
(function () {
    var quillEl = document.getElementById('quillEditor');
    if (!quillEl || typeof Quill === 'undefined') return;

    var quill = new Quill('#quillEditor', {
        theme: 'snow',
        modules: {
            toolbar: [
                [{ header: [1, 2, 3, false] }],
                ['bold', 'italic', 'underline', 'strike'],
                [{ color: [] }, { background: [] }],
                [{ list: 'ordered' }, { list: 'bullet' }],
                [{ align: [] }],
                ['link', 'image'],
                ['clean']
            ]
        }
    });

    // Pre-fill initial content for Edit page (stored as JSON in data attribute)
    var initData = quillEl.dataset.initialHtml;
    if (initData) {
        try { quill.root.innerHTML = JSON.parse(initData); } catch (_) { quill.root.innerHTML = initData; }
    }

    var bodyHtmlInput = document.getElementById('bodyHtmlInput');
    var form = document.querySelector('form');
    if (form && bodyHtmlInput) {
        form.addEventListener('submit', function () {
            bodyHtmlInput.value = quill.root.innerHTML;
        });
    }

    // Token insert — called by _TokenPanel buttons
    window.insertToken = function (token) {
        quill.focus();
        var range = quill.getSelection(true);
        quill.insertText(range.index, token);
        quill.setSelection(range.index + token.length);
        updatePreview();
    };

    // Chevron toggle
    var tokenPanel = document.getElementById('tokenPanel');
    if (tokenPanel) {
        tokenPanel.addEventListener('hide.bs.collapse', function () {
            var el = document.getElementById('tokenChevron');
            if (el) el.classList.replace('bx-chevron-up', 'bx-chevron-down');
        });
        tokenPanel.addEventListener('show.bs.collapse', function () {
            var el = document.getElementById('tokenChevron');
            if (el) el.classList.replace('bx-chevron-down', 'bx-chevron-up');
        });
    }

    // Live preview
    function updatePreview() {
        var frame = document.getElementById('previewFrame');
        if (!frame) return;
        var doc = frame.contentDocument || frame.contentWindow.document;
        doc.open(); doc.write(quill.root.innerHTML); doc.close();
    }
    quill.on('text-change', updatePreview);
    updatePreview();

    var improveBtns = document.querySelectorAll('.improve-ai-btn');
    var modalEl = document.getElementById('aiImproveModal');
    if (!improveBtns.length || !modalEl) return;

    var promptInput = document.getElementById('aiPromptInput');
    var resultOutput = document.getElementById('aiResultOutput');
    var statusEl = document.getElementById('aiImproveStatus');
    var runBtn = document.getElementById('aiRunImproveBtn');
    var copyBtn = document.getElementById('aiCopyResultBtn');
    var applyBtn = document.getElementById('aiApplyResultBtn');

    var activeContext = 'emailTemplate';
    var activeEndpoint = '/Updates/ImproveWithAi';

    function getAntiForgeryToken() {
        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        return token ? token.value : '';
    }

    function getModalInstance() {
        if (typeof bootstrap === 'undefined' || !bootstrap.Modal) return null;
        return bootstrap.Modal.getOrCreateInstance(modalEl);
    }

    function openModal() {
        var modal = getModalInstance();
        if (modal) modal.show();
        else modalEl.classList.add('show');
    }

    function closeModal() {
        var modal = getModalInstance();
        if (modal) modal.hide();
        else modalEl.classList.remove('show');
    }

    improveBtns.forEach(function (btn) {
        btn.addEventListener('click', function () {
            activeContext = this.dataset.aiContext || 'emailTemplate';
            activeEndpoint = this.dataset.aiEndpoint || '/Updates/ImproveWithAi';
            promptInput.value = this.dataset.defaultPrompt || 'Improve this email template for clarity, professionalism, and readability while preserving intent.';
            resultOutput.value = '';
            statusEl.textContent = '';
            openModal();
        });
    });

    runBtn.addEventListener('click', function () {
        var inputText = quill.root.innerHTML || '';
        if (!inputText.trim()) {
            statusEl.textContent = 'Add some content first.';
            return;
        }

        statusEl.textContent = 'Improving...';
        runBtn.disabled = true;

        fetch(activeEndpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify({
                inputText: inputText,
                prompt: promptInput.value || '',
                context: activeContext
            })
        })
            .then(function (res) {
                if (!res.ok) {
                    return res.json().then(function (err) { throw new Error(err.message || 'AI request failed.'); });
                }
                return res.json();
            })
            .then(function (data) {
                resultOutput.value = data.result || '';
                statusEl.textContent = 'Done.';
            })
            .catch(function (err) {
                statusEl.textContent = err.message || 'Something went wrong.';
            })
            .finally(function () {
                runBtn.disabled = false;
            });
    });

    copyBtn.addEventListener('click', function () {
        var text = resultOutput.value || '';
        if (!text.trim()) return;
        navigator.clipboard.writeText(text).then(function () {
            statusEl.textContent = 'Copied.';
        });
    });

    applyBtn.addEventListener('click', function () {
        var text = resultOutput.value || '';
        if (!text.trim()) return;
        quill.root.innerHTML = text;
        if (bodyHtmlInput) bodyHtmlInput.value = text;
        updatePreview();
        statusEl.textContent = 'Applied to editor.';
        closeModal();
    });
})();

// Preview page: copy buttons
(function () {
    var renderedHtml    = window._previewHtml    || '';
    var renderedSubject = window._previewSubject || '';
    if (!renderedHtml && !renderedSubject) return;

    // Render preview frame on load
    (function () {
        var frame = document.getElementById('previewFrame');
        if (!frame) return;
        frame.srcdoc = renderedHtml || '';
    })();

    // Render subject
    var subjectEl = document.getElementById('renderedSubjectDisplay');
    if (subjectEl) subjectEl.textContent = renderedSubject;

    function setupCopyBtn(id, getText, successLabel) {
        var btn = document.getElementById(id);
        if (!btn || btn.hasAttribute('disabled')) return;
        var orig = btn.innerHTML;
        btn.addEventListener('click', function () {
            var text = getText();
            if (typeof text === 'string') {
                navigator.clipboard.writeText(text).then(function () { etFlashBtn(btn, successLabel, orig); });
            } else if (text && typeof text.then === 'function') {
                text.then(function () { etFlashBtn(btn, successLabel, orig); });
            }
        });
    }

    setupCopyBtn('copyBodyBtn', function () { return renderedHtml; }, 'Copied!');
    setupCopyBtn('copySubjectBtn', function () { return renderedSubject; }, 'Subject copied!');

    var copyPlainBtn = document.getElementById('copyPlainBtn');
    if (copyPlainBtn && !copyPlainBtn.hasAttribute('disabled')) {
        var origPlain = copyPlainBtn.innerHTML;
        copyPlainBtn.addEventListener('click', function () {
            try {
                var htmlBlob = new Blob([renderedHtml], { type: 'text/html' });
                var scratch  = document.createElement('div');
                scratch.style.cssText = 'position:absolute;left:-9999px;top:-9999px;white-space:pre-wrap;';
                scratch.innerHTML = renderedHtml;
                document.body.appendChild(scratch);
                var plainText = scratch.innerText;
                document.body.removeChild(scratch);
                var textBlob = new Blob([plainText], { type: 'text/plain' });
                var item     = new ClipboardItem({ 'text/html': htmlBlob, 'text/plain': textBlob });
                navigator.clipboard.write([item]).then(function () {
                    etFlashBtn(copyPlainBtn, 'Copied (with links)!', origPlain);
                }).catch(function () {
                    navigator.clipboard.writeText(plainText).then(function () {
                        etFlashBtn(copyPlainBtn, 'Plain text copied!', origPlain);
                    });
                });
            } catch (e) {
                var ta  = document.createElement('textarea');
                var sc2 = document.createElement('div');
                sc2.innerHTML = renderedHtml;
                ta.value = sc2.innerText;
                document.body.appendChild(ta);
                ta.select();
                document.execCommand('copy');
                document.body.removeChild(ta);
                etFlashBtn(copyPlainBtn, 'Plain text copied!', origPlain);
            }
        });
    }
})();

// Index: DataTable init
(function () {
    var tbl = document.getElementById('templatesTable');
    if (tbl && typeof $ !== 'undefined' && $.fn.DataTable) {
        $(function () {
            $('#templatesTable').DataTable({ responsive: true, order: [[2, 'asc'], [0, 'asc']] });
        });
    }
})();

// BrandForm: dynamic brand links table
(function () {
    var tbody     = document.getElementById('brandLinksBody');
    if (!tbody) return;
    var jsonInput = document.getElementById('brandLinksJson');
    var addBtn    = document.getElementById('addBrandLinkRow');
    var form      = jsonInput.closest('form');

    var DEFAULT_LABELS = [
        'Bank Details - EN', 'Bank Details - PT', 'FNS', 'FNS - PT',
        'JAF', 'DOA', 'Joint FNS', 'FATCA', 'BOR', 'Corporate FNS'
    ];

    function escAttr(str) {
        return (str || '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function createRow(label, url) {
        var tr = document.createElement('tr');
        tr.innerHTML =
            '<td><input type="text" class="form-control form-control-sm link-label" value="' + escAttr(label) + '" placeholder="e.g. Bank Details - EN" /></td>' +
            '<td><input type="url"  class="form-control form-control-sm link-url"   value="' + escAttr(url)   + '" placeholder="https://..." /></td>' +
            '<td class="text-center"><button type="button" class="btn btn-sm btn-outline-danger remove-row"><i class="bx bx-trash"></i></button></td>';
        tr.querySelector('.remove-row').addEventListener('click', function () { tr.remove(); });
        tbody.appendChild(tr);
    }

    function serialise() {
        var rows = Array.from(tbody.querySelectorAll('tr')).map(function (tr) {
            return { Label: tr.querySelector('.link-label').value.trim(), Url: tr.querySelector('.link-url').value.trim() };
        }).filter(function (r) { return r.Label || r.Url; });
        jsonInput.value = JSON.stringify(rows);
    }

    var existing = [];
    try { existing = JSON.parse(jsonInput.value || '[]'); } catch (_) {}

    if (existing.length > 0) {
        existing.forEach(function (r) { createRow(r.Label || '', r.Url || ''); });
    } else {
        DEFAULT_LABELS.forEach(function (lbl) { createRow(lbl, ''); });
    }

    addBtn.addEventListener('click', function () { createRow('', ''); });
    form.addEventListener('submit', serialise);
})();
