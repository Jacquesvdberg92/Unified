// Schedule page scripts

// WeekView: edit modal population
(function () {
    var editModal = document.getElementById('editModal');
    if (editModal) {
        editModal.addEventListener('show.bs.modal', function (e) {
            var btn  = e.relatedTarget;
            var isWe = btn.dataset.weekend === 'true';
            document.getElementById('modal-agent-id').value           = btn.dataset.agentId;
            document.getElementById('modal-agent-name').textContent   = btn.dataset.agentName;
            document.getElementById('modal-date').value               = btn.dataset.date;
            document.getElementById('modal-date-display').textContent = (function (d) {
                return new Date(d + 'T00:00:00').toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'short', year: 'numeric' });
            })(btn.dataset.date);
            document.getElementById('modal-entry-id').value     = btn.dataset.entryId   || '0';
            document.getElementById('modal-type').value         = btn.dataset.entryType || '0';
            document.getElementById('modal-shift').value        = btn.dataset.shiftId   || '';
            document.getElementById('modal-custom-start').value = btn.dataset.customStart || '';
            document.getElementById('modal-custom-end').value   = btn.dataset.customEnd   || '';
            document.getElementById('modal-note').value         = btn.dataset.note || '';
            var ogWeekday = document.getElementById('og-weekday');
            var ogWeekend = document.getElementById('og-weekend');
            if (ogWeekday) ogWeekday.style.display = isWe ? 'none' : '';
            if (ogWeekend) ogWeekend.style.display = isWe ? ''     : 'none';
            schedToggle(btn.dataset.entryType || '0');
        });
    }
})();

function schedToggle(val) {
    var shiftRow  = document.getElementById('shiftRow');
    var customRow = document.getElementById('customRow');
    if (shiftRow)  shiftRow.style.display  = val === '0' ? '' : 'none';
    if (customRow) customRow.style.display = val === '1' ? '' : 'none';
}

// AgentView: my-day modal + clock
(function () {
    var myDayModal = document.getElementById('myDayModal');
    if (myDayModal) {
        myDayModal.addEventListener('show.bs.modal', function (e) {
            var btn       = e.relatedTarget;
            var isWeekend = btn.dataset.weekend === 'true';
            document.getElementById('my-date').value         = btn.dataset.date;
            document.getElementById('my-entry-id').value     = btn.dataset.entryId   || '0';
            document.getElementById('my-type').value         = btn.dataset.entryType || '0';
            document.getElementById('my-shift').value        = btn.dataset.shiftId   || '';
            document.getElementById('my-custom-start').value = btn.dataset.customStart || '';
            document.getElementById('my-custom-end').value   = btn.dataset.customEnd   || '';
            document.getElementById('my-note').value         = btn.dataset.note || '';
            var ogWeekday = document.getElementById('my-og-weekday');
            var ogWeekend = document.getElementById('my-og-weekend');
            if (ogWeekday) ogWeekday.style.display = isWeekend ? 'none' : '';
            if (ogWeekend) ogWeekend.style.display = isWeekend ? ''     : 'none';
        });
    }

    function agentTick() {
        var el = document.getElementById('agentLiveClock');
        if (el) el.textContent = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    }
    if (document.getElementById('agentLiveClock')) {
        agentTick();
        setInterval(agentTick, 1000);
    }
})();

// ReviewRequests: action modal
(function () {
    var actionModal = document.getElementById('actionModal');
    if (actionModal) {
        actionModal.addEventListener('show.bs.modal', function (e) {
            var btn       = e.relatedTarget;
            var rid       = btn.dataset.requestId;
            var agent     = btn.dataset.agent;
            var action    = btn.dataset.action;
            var isApprove = action === 'Approve';

            document.getElementById('actionRequestId').value      = rid;
            document.getElementById('actionTitle').textContent    = action + ' Request';
            document.getElementById('actionBody').textContent     = action + ' request from ' + agent + '?';

            var form = document.getElementById('actionForm');
            form.action = '/Schedule/' + action + 'Request';

            var confirmBtn = document.getElementById('actionBtn');
            confirmBtn.className    = 'btn btn-sm ' + (isApprove ? 'btn-success' : 'btn-danger');
            confirmBtn.textContent  = action;
        });
    }
})();

// MyRequests: schedule field toggle
function toggleScheduleFields() {
    var type             = document.getElementById('reqType');
    if (!type) return;
    var val              = type.value;
    var isScheduleChange = val === '2';
    var scheduleTimeRow  = document.getElementById('scheduleTimeRow');
    var endDateCol       = document.getElementById('endDateCol');
    var startDate        = document.getElementById('startDate');
    var endDate          = document.getElementById('endDate');

    if (scheduleTimeRow) scheduleTimeRow.style.display = isScheduleChange ? '' : 'none';
    if (val === '1' || val === '2') {
        if (endDateCol) endDateCol.style.display = 'none';
        if (startDate) {
            startDate.addEventListener('change', function () {
                if (endDate) endDate.value = this.value;
            });
        }
    } else {
        if (endDateCol) endDateCol.style.display = '';
    }
}

if (document.getElementById('reqType')) {
    toggleScheduleFields();
}

// WeekendWheel: spin animation
(function () {
    var spinBtn   = document.getElementById('spinBtn');
    if (!spinBtn) return;

    var agents      = JSON.parse(document.getElementById('agentData')?.textContent || '[]');
    var display     = document.getElementById('wheelDisplay');
    var offerForm   = document.getElementById('offerForm');
    var selectedId  = document.getElementById('selectedAgentId');
    var selectedName = document.getElementById('selectedAgentName');
    var chosen      = null;

    spinBtn.addEventListener('click', function () {
        if (!agents.length) return;
        spinBtn.disabled = true;

        var ticks = 20 + Math.floor(Math.random() * 15);
        var idx   = 0;
        var delay = 60;

        function tick() {
            display.textContent = agents[idx % agents.length].name;
            idx++;
            ticks--;
            delay += 8;
            if (ticks > 0) {
                setTimeout(tick, delay);
            } else {
                chosen = agents[(idx - 1) % agents.length];
                display.textContent      = chosen.name;
                selectedId.value         = chosen.id;
                selectedName.textContent = chosen.name;
                offerForm.style.display  = '';
            }
        }
        tick();
    });

    window.resetWheel = function () {
        chosen = null;
        display.textContent     = '\u2014';
        offerForm.style.display = 'none';
        spinBtn.disabled        = false;
    };
})();
