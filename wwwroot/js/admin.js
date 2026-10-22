// Admin shared table initializations, again after an in-place update (js/inplace.js)
// swaps the table in
function initAdminTables() {
    $('[data-admin-datatable]').each(function () {
        const $table = $(this);
        if ($.fn.DataTable.isDataTable(this)) {
            return;
        }

        const orderColumn = $table.data('order-column');
        const orderDir = $table.data('order-dir') || 'asc';
        const pageLength = $table.data('page-length') || 10;

        $table.DataTable({
            responsive: true,
            pageLength: pageLength,
            order: orderColumn !== undefined ? [[orderColumn, orderDir]] : []
        });
    });
}

$(initAdminTables);
document.addEventListener('inplace:load', initAdminTables);
