// Vault page scripts

// Password visibility toggle — used by AddEntry, EditEntry
(function () {
    var togglePwd = document.getElementById('togglePwd');
    if (togglePwd) {
        togglePwd.addEventListener('click', function () {
            var f = document.getElementById('plainPassword');
            f.type = f.type === 'password' ? 'text' : 'password';
            this.textContent = f.type === 'password' ? '\uD83D\uDD12' : '\uD83D\uDE48';
        });
    }
})();

// BulkProvision password toggle
(function () {
    var toggleBulkPwd = document.getElementById('toggleBulkPwd');
    if (toggleBulkPwd) {
        toggleBulkPwd.addEventListener('click', function () {
            var f = document.getElementById('bulkPwd');
            f.type = f.type === 'password' ? 'text' : 'password';
            this.textContent = f.type === 'password' ? '\uD83D\uDD12' : '\uD83D\uDE48';
        });

        var selAll = document.getElementById('selAll');
        var clrAll = document.getElementById('clrAll');
        if (selAll) selAll.addEventListener('click', function () {
            document.querySelectorAll('.agent-cb').forEach(function (c) { c.checked = true; });
        });
        if (clrAll) clrAll.addEventListener('click', function () {
            document.querySelectorAll('.agent-cb').forEach(function (c) { c.checked = false; });
        });
    }
})();

// BulkUpdatePassword toggle + select all
(function () {
    var toggleRotPwd = document.getElementById('toggleRotPwd');
    if (toggleRotPwd) {
        toggleRotPwd.addEventListener('click', function () {
            var f = document.getElementById('rotPwd');
            f.type = f.type === 'password' ? 'text' : 'password';
            this.textContent = f.type === 'password' ? '\uD83D\uDD12' : '\uD83D\uDE48';
        });

        var selAll2 = document.getElementById('selAll');
        var clrAll2 = document.getElementById('clrAll');
        if (selAll2) selAll2.addEventListener('click', function () {
            document.querySelectorAll('.agent-cb2').forEach(function (c) { c.checked = true; });
        });
        if (clrAll2) clrAll2.addEventListener('click', function () {
            document.querySelectorAll('.agent-cb2').forEach(function (c) { c.checked = false; });
        });
    }
})();

// MyVault: reveal and copy password
(function () {
    var antiForgeryForm = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
    if (!antiForgeryForm) return;
    var token = antiForgeryForm.value;

    document.querySelectorAll('.btn-reveal').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            var entryId   = this.dataset.entryId;
            var fieldId   = this.dataset.fieldId;
            var actionVal = this.dataset.action || 'View';
            var field     = document.getElementById(fieldId);

            if (field.type === 'text') {
                field.type = 'password';
                this.textContent = '\uD83D\uDD12';
                return;
            }

            var resp = await fetch('/Vault/Reveal', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token
                },
                body: 'id=' + entryId + '&action=' + actionVal
            });

            if (resp.ok) {
                var data = await resp.json();
                field.value = data.password;
                field.type  = 'text';
                this.textContent = '\uD83D\uDE48';
                var self = this;
                setTimeout(function () {
                    field.type  = 'password';
                    field.value = '';
                    self.textContent = '\uD83D\uDD12';
                }, 30000);
            }
        });
    });

    document.querySelectorAll('.btn-copy').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            var entryId  = this.dataset.entryId;
            var copyType = this.dataset.type;
            var fieldId  = this.dataset.fieldId;
            var field    = document.getElementById(fieldId);
            var text     = '';

            if (copyType === 'username') {
                text = field.value;
            } else {
                var resp = await fetch('/Vault/Reveal', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded',
                        'RequestVerificationToken': token
                    },
                    body: 'id=' + entryId + '&action=Copy'
                });
                if (!resp.ok) return;
                var data = await resp.json();
                text = data.password;
            }

            await navigator.clipboard.writeText(text);
            var orig = this.innerHTML;
            var self = this;
            this.innerHTML = '\u2714 Copied';
            setTimeout(function () {
                self.innerHTML = orig;
                navigator.clipboard.writeText('');
            }, 30000);
        });
    });
})();
