// Attendance page scripts

function tick() {
    var now = new Date();
    var clockEl = document.getElementById('liveClock');
    var dateEl  = document.getElementById('liveDate');
    if (clockEl) clockEl.textContent = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    if (dateEl)  dateEl.textContent  = now.toLocaleDateString([], { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
}

if (document.getElementById('liveClock')) {
    tick();
    setInterval(tick, 1000);
}

// AttendanceReport: toggle custom date range
function toggleCustomDates(val) {
    var el = document.getElementById('customDateRange');
    if (el) el.style.display = val === 'custom' ? 'flex' : 'none';
}
