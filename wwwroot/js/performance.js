// Performance: dynamic review item rows
(function () {
    var addItemBtn = document.getElementById('addItemBtn');
    if (!addItemBtn) return;

    var container = document.getElementById('itemsContainer');
    var itemIndex = 0;

    var catDataEl = document.getElementById('categoryOptionsData');
    var categoryOptions = catDataEl ? JSON.parse(catDataEl.textContent || '[]') : [];

    function addItem() {
        var div = document.createElement('div');
        div.className = 'card shadow-sm mb-2 item-card';
        div.innerHTML =
            '<div class="card-body row g-2">' +
                '<div class="col-md-2">' +
                    '<label class="form-label small">Category</label>' +
                    '<select name="category" class="form-select form-select-sm">' +
                        categoryOptions.map(function (o) { return '<option value="' + o.value + '">' + o.text + '</option>'; }).join('') +
                    '</select>' +
                '</div>' +
                '<div class="col-md-2">' +
                    '<label class="form-label small">Reference ID</label>' +
                    '<input type="text" name="refId" class="form-control form-control-sm" placeholder="Ticket / Chat ID" required />' +
                '</div>' +
                '<div class="col-md-1">' +
                    '<label class="form-label small">Rating</label>' +
                    '<input type="number" name="rating" min="1" max="10" value="7" class="form-control form-control-sm" required />' +
                '</div>' +
                '<div class="col-md-2">' +
                    '<label class="form-label small">Positive</label>' +
                    '<input type="text" name="positive" class="form-control form-control-sm" />' +
                '</div>' +
                '<div class="col-md-2">' +
                    '<label class="form-label small">Improvement</label>' +
                    '<input type="text" name="negative" class="form-control form-control-sm" />' +
                '</div>' +
                '<div class="col-md-2">' +
                    '<label class="form-label small">Action Note</label>' +
                    '<input type="text" name="actionNote" class="form-control form-control-sm" />' +
                '</div>' +
                '<div class="col-md-1 d-flex align-items-end">' +
                    '<div class="form-check mb-1">' +
                        '<input type="checkbox" name="actionRequired" value="true" class="form-check-input" id="ar_' + itemIndex + '" />' +
                        '<label class="form-check-label small" for="ar_' + itemIndex + '">Action?</label>' +
                    '</div>' +
                '</div>' +
                '<div class="col-12 text-end">' +
                    '<button type="button" class="btn btn-outline-danger btn-sm remove-btn">Remove</button>' +
                '</div>' +
            '</div>';
        div.querySelector('.remove-btn').addEventListener('click', function () { div.remove(); });
        container.appendChild(div);
        itemIndex++;
    }

    addItemBtn.addEventListener('click', addItem);
    addItem();
})();
