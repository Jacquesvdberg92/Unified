// Reports: FTD bar chart (Detail page)
(function () {
    var chartDataEl = document.getElementById('ftdChartData');
    if (!chartDataEl || typeof ApexCharts === 'undefined') return;
    var chartData = JSON.parse(chartDataEl.textContent || '{}');
    if (!chartData.labels || !chartData.data) return;
    new ApexCharts(document.getElementById('ftdChart'), {
        chart:  { type: 'bar', height: 250, toolbar: { show: false } },
        series: [{ name: 'FTDs', data: chartData.data }],
        xaxis:  { categories: chartData.labels },
        colors: ['#198754'],
        plotOptions: { bar: { borderRadius: 4, distributed: true } },
        legend: { show: false }
    }).render();
})();

// Reports: CSV export from agent table
(function () {
    var csvBtn = document.getElementById('csvBtn');
    if (!csvBtn) return;
    csvBtn.addEventListener('click', function () {
        var rows = [['Agent', 'Language', 'Chats', 'Tickets', 'Calls', 'FTD', 'Avg Review']];
        document.querySelectorAll('#agentTable tbody tr').forEach(function (tr) {
            rows.push(Array.from(tr.querySelectorAll('td')).slice(0, 7).map(function (td) { return td.innerText.trim(); }));
        });
        var csv  = rows.map(function (r) { return r.map(function (c) { return '"' + c + '"'; }).join(','); }).join('\n');
        var blob = new Blob([csv], { type: 'text/csv' });
        var a    = document.createElement('a');
        a.href     = URL.createObjectURL(blob);
        a.download = 'report.csv';
        a.click();
    });
})();

// Reports: dynamic agent row builder (Submit page)
(function () {
    var addRowBtn = document.getElementById('addRow');
    if (!addRowBtn) return;

    var agentDataEl = document.getElementById('agentOptionsData');
    var agentOptions = agentDataEl ? JSON.parse(agentDataEl.textContent || '[]') : [];

    function addRow() {
        var tbody = document.getElementById('agentRows');
        var tr    = document.createElement('tr');
        tr.innerHTML =
            '<td>' +
                '<select name="agentId" class="form-select form-select-sm" required>' +
                    '<option value="">\u2014 Select \u2014</option>' +
                    agentOptions.map(function (a) { return '<option value="' + a.value + '">' + a.text + '</option>'; }).join('') +
                '</select>' +
            '</td>' +
            '<td><input type="number" name="chats"    min="0" value="0" class="form-control form-control-sm" style="width:75px" /></td>' +
            '<td><input type="number" name="tickets"  min="0" value="0" class="form-control form-control-sm" style="width:75px" /></td>' +
            '<td><input type="number" name="calls"    min="0" value="0" class="form-control form-control-sm" style="width:75px" /></td>' +
            '<td><input type="number" name="ftd"      min="0" value="0" class="form-control form-control-sm" style="width:75px" /></td>' +
            '<td><input type="text"   name="language" placeholder="EN" class="form-control form-control-sm" style="width:75px" /></td>' +
            '<td><button type="button" class="btn btn-outline-danger btn-sm remove-row">\u2715</button></td>';
        tr.querySelector('.remove-row').addEventListener('click', function () { tr.remove(); });
        tbody.appendChild(tr);
    }

    addRowBtn.addEventListener('click', addRow);
    addRow();
})();
