// Updates: Quill rich-text editor for update body
(function () {
    var quillEditor = document.getElementById('quillEditor');
    if (!quillEditor || typeof Quill === 'undefined') return;

    var quill = new Quill('#quillEditor', {
        theme: 'snow',
        modules: {
            toolbar: [
                ['bold', 'italic', 'underline', 'strike'],
                [{ list: 'ordered' }, { list: 'bullet' }],
                ['link'],
                ['clean']
            ]
        }
    });

    var bodyInput = document.getElementById('bodyInput');
    if (bodyInput && bodyInput.value) {
        quill.root.innerHTML = bodyInput.value;
    }

    var form = document.querySelector('form');
    if (form) {
        form.addEventListener('submit', function () {
            if (bodyInput) bodyInput.value = quill.root.innerHTML;
        });
    }

    var improveBtns = document.querySelectorAll('.improve-ai-btn');
    var modalEl = document.getElementById('aiImproveModal');
    if (!improveBtns.length || !modalEl) return;

    var promptInput = document.getElementById('aiPromptInput');
    var resultOutput = document.getElementById('aiResultOutput');
    var statusEl = document.getElementById('aiImproveStatus');
    var runBtn = document.getElementById('aiRunImproveBtn');
    var copyBtn = document.getElementById('aiCopyResultBtn');
    var applyBtn = document.getElementById('aiApplyResultBtn');

    var activeContext = 'update';
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
            activeContext = this.dataset.aiContext || 'update';
            activeEndpoint = this.dataset.aiEndpoint || '/Updates/ImproveWithAi';
            promptInput.value = this.dataset.defaultPrompt || 'Rewrite this update to sound friendly, clear, and professional.';
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
        if (bodyInput) bodyInput.value = text;
        statusEl.textContent = 'Applied to editor.';
        closeModal();
    });
})();
