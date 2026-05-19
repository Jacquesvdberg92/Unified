// Home / Dashboard customisation
(function () {
    var activeList   = document.getElementById('active-list');
    var catalogList  = document.getElementById('catalog-list');
    var hiddenInputs = document.getElementById('hidden-inputs');
    var emptyHint    = document.getElementById('empty-hint');
    var saveForm     = document.getElementById('save-form');

    if (!activeList) return;

    // Drag to reorder
    if (typeof Sortable !== 'undefined') {
        Sortable.create(activeList, { handle: '.drag-handle', animation: 150 });
    }

    // Add widget from catalog
    catalogList.addEventListener('click', function (e) {
        var btn = e.target.closest('.add-widget-btn');
        if (!btn || btn.disabled) return;

        var key   = btn.dataset.key;
        var title = btn.dataset.title;
        var icon  = btn.dataset.icon;

        var li = document.createElement('li');
        li.className      = 'list-group-item d-flex align-items-center gap-3 px-3 py-2 active-widget-row';
        li.dataset.key     = key;
        li.dataset.colspan = '6';
        li.innerHTML =
            '<i class="bx bx-menu text-muted drag-handle" style="cursor:grab;"></i>' +
            '<i class="' + icon + ' fs-5 text-primary"></i>' +
            '<span class="flex-grow-1 fw-semibold small">' + title + '</span>' +
            '<select class="form-select form-select-sm width-select" style="width:110px;">' +
                '<option value="4">1/3 Width</option>' +
                '<option value="6" selected>Half Width</option>' +
                '<option value="8">2/3 Width</option>' +
                '<option value="12">Full Width</option>' +
            '</select>' +
            '<button type="button" class="btn btn-sm btn-outline-danger remove-widget-btn">' +
                '<i class="bx bx-trash"></i>' +
            '</button>';
        activeList.appendChild(li);

        btn.disabled = true;
        btn.closest('li').classList.add('opacity-50');
        if (emptyHint) emptyHint.classList.add('d-none');
    });

    // Remove widget
    activeList.addEventListener('click', function (e) {
        var btn = e.target.closest('.remove-widget-btn');
        if (!btn) return;
        var row = btn.closest('.active-widget-row');
        var key = row.dataset.key;
        var catBtn = catalogList.querySelector('[data-key="' + key + '"]');
        if (catBtn) {
            catBtn.disabled = false;
            catBtn.closest('li').classList.remove('opacity-50');
        }
        row.remove();
        if (!activeList.querySelector('.active-widget-row') && emptyHint) {
            emptyHint.classList.remove('d-none');
        }
    });

    // Build hidden inputs on submit
    saveForm.addEventListener('submit', function () {
        hiddenInputs.innerHTML = '';
        document.querySelectorAll('#active-list .active-widget-row').forEach(function (row) {
            var key  = row.dataset.key;
            var span = (row.querySelector('.width-select') || {}).value || '6';
            hiddenInputs.innerHTML +=
                '<input type="hidden" name="widgetKeys" value="' + key + '">' +
                '<input type="hidden" name="colSpans"   value="' + span + '">';
        });
    });
})();
