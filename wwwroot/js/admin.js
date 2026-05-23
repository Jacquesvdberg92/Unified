// Admin shared table initializations
$(function () {
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
});
