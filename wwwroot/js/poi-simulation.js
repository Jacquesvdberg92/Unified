// PoiSimulation: mark-received modal population
(function () {
    var modal = document.getElementById('markReceivedModal');
    if (modal) {
        modal.addEventListener('show.bs.modal', function (e) {
            var btn = e.relatedTarget;
            document.getElementById('mrSimId').value             = btn.dataset.simId;
            document.getElementById('mrReturnUrl').value         = btn.dataset.returnUrl;
            document.getElementById('mrClientId').textContent    = btn.dataset.clientId;
            document.getElementById('mrBrand').textContent       = btn.dataset.brand;
            document.getElementById('mrSimulatedAt').textContent = btn.dataset.simulatedAt;
        });
    }
})();
