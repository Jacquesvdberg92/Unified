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
})();
