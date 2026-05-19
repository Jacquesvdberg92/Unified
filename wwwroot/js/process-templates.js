// Process Templates scripts

// _TemplateForm: insert blank placeholder
function insertBlank() {
    var ta    = document.getElementById('bodyText');
    var start = ta.selectionStart;
    var end   = ta.selectionEnd;
    ta.value           = ta.value.slice(0, start) + '[BLANK]' + ta.value.slice(end);
    ta.selectionStart  = ta.selectionEnd = start + 7;
    ta.focus();
}

// _TemplateForm: Quill guidance editor
(function () {
    var guidanceEditor = document.getElementById('guidanceEditor');
    if (!guidanceEditor || typeof Quill === 'undefined') return;
    var quill      = new Quill('#guidanceEditor', { theme: 'snow' });
    var hiddenNotes = document.getElementById('GuidanceNotes');
    if (hiddenNotes && hiddenNotes.value) quill.root.innerHTML = hiddenNotes.value;
    var form = document.querySelector('form');
    if (form) {
        form.addEventListener('submit', function () {
            if (hiddenNotes) hiddenNotes.value = quill.root.innerHTML;
        });
    }
})();

// View: highlight blanks, copy, download, guidance chevron
(function () {
    var bodyEl = document.getElementById('templateBody');
    if (!bodyEl) return;

    var rawEl = document.getElementById('templateBodyRaw');
    if (!rawEl) return;
    var raw = rawEl.textContent;

    bodyEl.innerHTML = raw.replace(/\[BLANK\]/g, '<span class="template-blank">[BLANK]</span>');

    var btnCopy = document.getElementById('btnCopy');
    if (btnCopy) {
        btnCopy.addEventListener('click', async function () {
            await navigator.clipboard.writeText(raw);
            btnCopy.innerHTML = '<i class="bx bx-check me-1"></i>Copied!';
            setTimeout(function () {
                btnCopy.innerHTML = '<i class="bx bx-copy me-1"></i>Copy to Clipboard';
            }, 2000);
        });
    }

    var btnDownload = document.getElementById('btnDownload');
    if (btnDownload) {
        var blob     = new Blob([raw], { type: 'text/plain' });
        btnDownload.href     = URL.createObjectURL(blob);
        btnDownload.download = (btnDownload.dataset.filename || 'template') + '.txt';
    }

    var guidanceBody = document.getElementById('guidanceBody');
    if (guidanceBody) {
        guidanceBody.addEventListener('show.bs.collapse', function () {
            var el = document.getElementById('guidanceChevron');
            if (el) el.className = 'bx bx-chevron-up';
        });
        guidanceBody.addEventListener('hide.bs.collapse', function () {
            var el = document.getElementById('guidanceChevron');
            if (el) el.className = 'bx bx-chevron-down';
        });
    }
})();
