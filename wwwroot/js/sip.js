(function () {
    const root = document.querySelector('[data-sip-details]');
    if (!root) return;

    const sipId = root.getAttribute('data-sip-id');
    if (!sipId) return;

    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenInput) return;

    const scoreEl = root.querySelector('[data-sip-score]');
    const upvotesEl = root.querySelector('[data-sip-upvotes]');
    const downvotesEl = root.querySelector('[data-sip-downvotes]');

    async function submitVote(isUpvote) {
        const res = await fetch(`/Sip/Vote/${sipId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': tokenInput.value
            },
            body: JSON.stringify({ isUpvote })
        });

        const data = await res.json().catch(() => null);
        if (!res.ok || !data || !data.success) {
            alert(data?.error || 'Unable to cast vote.');
            return;
        }

        if (scoreEl) scoreEl.textContent = String(data.netScore);
        if (upvotesEl) upvotesEl.textContent = String(data.upvotes);
        if (downvotesEl) downvotesEl.textContent = String(data.downvotes);
    }

    root.querySelectorAll('[data-vote]').forEach(btn => {
        btn.addEventListener('click', function () {
            submitVote(this.getAttribute('data-vote') === 'up');
        });
    });
})();
