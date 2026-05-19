// Admin shared table initializations
$(function () {
    if (document.getElementById('requestsTable')) {
        $('#requestsTable').DataTable({ responsive: true, order: [[4, 'desc']] });
    }
    if (document.getElementById('usersTable')) {
        $('#usersTable').DataTable({ responsive: true });
    }
});
