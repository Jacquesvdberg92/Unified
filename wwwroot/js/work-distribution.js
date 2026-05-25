// WorkDistribution: swap modal population + mention highlighting
(function () {
    // Swap modal
    var swapModal = document.getElementById('swapModal');
    if (swapModal) {
        swapModal.addEventListener('show.bs.modal', function (e) {
            var btn = e.relatedTarget;
            document.getElementById('swapSlotId').value          = btn.dataset.slotId;
            document.getElementById('swapSlotLabel').textContent = btn.dataset.slotLabel;
            document.getElementById('swapReturnDate').value      = btn.dataset.date;
        });
    }

    // Highlight @mentions for the current user in all .work-dist-body elements
    var container = document.querySelector('[data-current-user]');
    var currentUser = container ? (container.dataset.currentUser || '').trim() : '';

    if (currentUser) {
        var escaped = currentUser.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        var re = new RegExp('@(' + escaped + ')(?=[\\s,.:;!?\\r\\n]|$)', 'gi');

        document.querySelectorAll('pre.work-dist-body').forEach(function (pre) {
            pre.innerHTML = pre.textContent.replace(re, '<span class="mention-me">@$1</span>');
        });
    }
})();
