// CsLiveHelp: swap modal population
(function () {
    var swapModal = document.getElementById('swapModal');
    if (swapModal) {
        swapModal.addEventListener('show.bs.modal', function (e) {
            var btn = e.relatedTarget;
            document.getElementById('swapSlotId').value           = btn.dataset.slotId;
            document.getElementById('swapSlotLabel').textContent  = btn.dataset.slotLabel;
            document.getElementById('swapReturnDate').value       = btn.dataset.date;
        });
    }
})();
