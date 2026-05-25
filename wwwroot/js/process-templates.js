// Process Templates scripts

var bodyQuill;

// _TemplateForm: insert blank placeholder
function insertBlank() {
    if (bodyQuill) {
        var range = bodyQuill.getSelection(true);
        var index = range ? range.index : bodyQuill.getLength();
        bodyQuill.insertText(index, '[BLANK]');
        bodyQuill.setSelection(index + 7, 0);
        bodyQuill.focus();
        return;
    }

    var ta = document.getElementById('bodyText');
    if (!ta) return;
    var start = ta.selectionStart;
    var end = ta.selectionEnd;
    ta.value = ta.value.slice(0, start) + '[BLANK]' + ta.value.slice(end);
    ta.selectionStart = ta.selectionEnd = start + 7;
    ta.focus();
}

// _TemplateForm: Quill editors
(function () {
    if (typeof Quill === 'undefined') return;

    function extractPlainTextWithLists(quill) {
        var root = quill.root;
        if (!root) return '';

        var blocks = root.querySelectorAll('p, ol, ul');
        var lines = [];

        blocks.forEach(function (block) {
            var tag = block.tagName;

            if (tag === 'OL') {
                var items = block.querySelectorAll(':scope > li');
                items.forEach(function (li, index) {
                    lines.push((index + 1) + '. ' + li.innerText.trim());
                });
                return;
            }

            if (tag === 'UL') {
                var items = block.querySelectorAll(':scope > li');
                items.forEach(function (li) {
                    lines.push('- ' + li.innerText.trim());
                });
                return;
            }

            var text = block.innerText;
            lines.push(text ? text.trimEnd() : '');
        });

        return lines.join('\n').replace(/\n{3,}/g, '\n\n').trimEnd();
    }

    var bodyEditor = document.getElementById('bodyEditor');
    var hiddenBody = document.getElementById('BodyText');
    if (bodyEditor && hiddenBody) {
        bodyQuill = new Quill('#bodyEditor', {
            theme: 'snow',
            modules: {
                toolbar: [
                    ['bold', 'italic', 'underline'],
                    [{ list: 'ordered' }, { list: 'bullet' }],
                    ['clean']
                ]
            }
        });
        bodyQuill.root.innerText = hiddenBody.value || '';
    }

    var guidanceEditor = document.getElementById('guidanceEditor');
    var hiddenNotes = document.getElementById('GuidanceNotes');
    var guidanceQuill = null;
    if (guidanceEditor && hiddenNotes) {
        guidanceQuill = new Quill('#guidanceEditor', {
            theme: 'snow',
            modules: {
                toolbar: [
                    ['bold', 'italic', 'underline'],
                    [{ list: 'ordered' }, { list: 'bullet' }],
                    ['clean']
                ]
            }
        });
        if (hiddenNotes.value) guidanceQuill.root.innerHTML = hiddenNotes.value;
    }

    var form = document.querySelector('form');
    if (form) {
        form.addEventListener('submit', function () {
            if (hiddenBody && bodyQuill) hiddenBody.value = extractPlainTextWithLists(bodyQuill);
            if (hiddenNotes && guidanceQuill) hiddenNotes.value = guidanceQuill.root.innerHTML;
        });
    }
})();

// View: highlight blanks, copy, download, guidance chevron
(function () {
    var bodyEl = document.getElementById('templateBody');
    if (!bodyEl) return;

    var rawEl = document.getElementById('templateBodyRaw');
    if (!rawEl) return;

    var rawJson = rawEl.textContent || '""';
    var raw;
    try {
        raw = JSON.parse(rawJson);
    } catch {
        raw = rawJson;
    }

    var encoded = raw
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');

    bodyEl.innerHTML = encoded.replace(/\[BLANK\]/g, '<span class="template-blank">[BLANK]</span>');

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
