# CS Live Help: Real-time Live Updates Architecture

## Overview

This document describes the real-time update flow for the three CS Live Help pages using SignalR, including how comments are synchronized live, how notification counts update, and how modal lifecycle issues are prevented.

## Three Pages

### 1. **Board.cshtml** (CS Agent View)
- **URL**: `/CsLiveHelp/Board`
- **Purpose**: Kanban-style board showing all non-internal requests (5 columns: Open, InProgress, OnGoing, Escalated, Completed)
- **Users**: Customer Support agents only
- **SignalR Group**: `cs-board`
- **Card Partial**: `_CsBoardCard.cshtml`
- **Key Features**:
  - Drag-drop card movement
  - Comment threads with live updates
  - Escalate to team actions
  - Smart actions (Reset Password, Log Simulation, Mark Passed)

### 2. **Requests.cshtml** (Account Manager View)
- **URL**: `/CsLiveHelp/Requests`
- **Purpose**: AM's own requests + read-only view of other AMs' open requests
- **Users**: Account Managers
- **SignalR Group**: `am-{userId}`
- **Card Partial**: `_CsRequestCard.cshtml`
- **Key Features**:
  - Create/edit/delete own requests
  - Comment threads with live CS responses
  - 3-column board (Open, InProgress/Escalated, Completed)

### 3. **RequestsAllBrands.cshtml** (Internal CS View)
- **URL**: `/CsLiveHelp/RequestsAllBrands`
- **Purpose**: Internal-only board with all requests (including internal-created ones)
- **Users**: CS Team Leaders, Brand Managers, admins
- **SignalR Group**: `cs-allbrands`
- **Card Partial**: `_InternalBoardCard.cshtml`
- **Key Features**:
  - Internal comment threads with mentions
  - Document/image uploads (≤20MB for docs, ≤5MB for images)
  - Team escalation resolution
  - 4-column board (Open, InProgress, OnGoing, Completed)

---

## SignalR Event Flow

### Real-time Events

All three pages receive and handle these events via the shared `cslivehelp.js`:

| Event | Payload | Handling |
|-------|---------|----------|
| **CardAdded** | `{id, brandName, requestType, status, assignedTo, isInternal}` | Fetch card partial, prepend to column, update count |
| **CardUpdated** | `{id, brandName, requestType, status, assignedTo}` | Flash card border, log to console |
| **CardStatusChanged** | `{id, newStatus, assignedTo}` | Move card to new column, update badge, update counts |
| **CardDeleted** | `{id}` | Remove card from DOM, remove modals, update counts |
| **CommentAdded** | `{requestId, author, body, imagePath, isSystem, createdAt}` | Append comment to thread, update comment count |
| **RequestNotification** | `{type, requestId, brandName, requestType, contextType}` | Send notification (if NotificationManager available) |
| **CommentNotification** | `{requestId, author, contextType}` | Send comment notification |
| **MentionNotification** | `{requestId, author, contextType}` | Send mention notification (internal only) |

### Comment Count Updates

**Comment count badges appear in the card HTML**:

- **Board.cshtml (_CsBoardCard.cshtml)**: Uses `.comment-count` class
  ```html
  <span class="comment-count">5</span> comment(s)
  ```

- **Requests.cshtml (_CsRequestCard.cshtml)**: Uses `.comment-count` class (conditional, only shown if count > 0)
  ```html
  <span class="comment-count">3</span> comment(s)
  ```

- **RequestsAllBrands.cshtml (_InternalBoardCard.cshtml)**: Uses `.int-comment-count` class
  ```html
  <span class="int-comment-count">2</span> comment(s)
  ```

**When `CommentAdded` event fires**:
1. Shared handler in `cslivehelp.js` finds the card element by `data-card-id`
2. Tries both selectors (`.comment-count` and `.int-comment-count`)
3. Increments the numeric value
4. Shows the comment count button if it was hidden (first comment on a card)
5. Internal handler in `cslivehelp-internal.js` additionally targets `.int-comment-count`

---

## Thread Body Detection (Modal Content)

### Modal Structure

Each page has its own modal ID pattern:

| Page | Modal ID | Thread Body ID |
|------|----------|----------------|
| Board | `#csCommentModal-{id}` | `#threadBody-{id}` or `.thread-body` div |
| Requests | `#commentModal-{id}` | `#threadBody-{id}` or `.thread-body` div |
| Internal | `#intCommentModal-{id}` | `.thread-body` div (internal only) |

### Detection Logic (`findThreadBodyElement` in cslivehelp.js)

The function tries patterns in order, returning the first found:

1. **Pattern 1**: `#csCommentModal-{id} .thread-body` (Board)
2. **Pattern 2**: `#intCommentModal-{id} .thread-body` (Internal)
3. **Pattern 3**: `#commentModal-{id} .thread-body` (Requests)
4. **Pattern 4**: `#intCommentModal-{id} .modal-body .border.rounded` (Internal fallback)
5. **Pattern 5**: `#threadBody-{id}` (Any page placeholder)

**Critical**: This function **does not check visibility** (`offsetParent`). Comments must be appended even if the modal is closed, because:
- The modal may be dynamically re-fetched on re-open
- The thread needs to be ready when the user opens it again

### Internal Thread Body Detection

`cslivehelp-internal.js` provides `findInternalThreadBodyElement()` which is identical but targeted:
- Looks for `.thread-body` inside `#intCommentModal-{id}`
- Falls back to `.modal-body .border.rounded`
- Returns the modal-body itself if no thread container exists (will create one on first comment)

---

## Modal Lifecycle & Stuck Loading Fix

### Problem: Stuck Loading Spinners

Previously, when a comment modal closed and re-opened, the modal-body might retain a loading spinner, making it appear "stuck" indefinitely.

### Solution: Proper Modal State Management

#### On Modal Show (`show.bs.modal` event)
1. **Cleanup backdrops**: Remove stale modal backdrop elements
2. **For AM Requests page only**: Fetch fresh thread HTML from `/CsLiveHelp/AmCommentThread/{id}`
   - This ensures new CS comments appear without page refresh
   - Replaces placeholder `<p>` with a proper `.thread-body` div if needed
   - Scrolls to bottom when content is loaded

#### On Modal Hide (`hide.bs.modal` event)
1. **Blur focused elements**: Clear focus to prevent accessibility violations
   - This fires BEFORE Bootstrap applies `aria-hidden`, preventing focus-trap warnings

#### On Modal Hidden (`hidden.bs.modal` event)
1. **Final cleanup**: Remove stale backdrops again if multiple modals were stacked

### Code Flow

```javascript
document.addEventListener('show.bs.modal', function (e) {
	const modal = e.target;
	// 1. Cleanup backdrops
	cleanupStaleModalState();

	// 2. For AM Requests, fetch fresh thread
	if (modal.id.startsWith('commentModal-')) {
		fetch('/CsLiveHelp/AmCommentThread/' + requestId)
			.then(html => { /* replace threadBody with fresh content */ })
			.catch(() => { /* keep existing */ });
	}
});

document.addEventListener('hide.bs.modal', function (e) {
	const modal = e.target;
	// Clear focus before aria-hidden applied
	modal.querySelector(':focus')?.blur();
});

document.addEventListener('hidden.bs.modal', function (e) {
	// Final cleanup
	cleanupStaleModalState();
});
```

---

## Form Submission & AJAX Handling

### Problem: Duplicate Submissions

Forms could be submitted multiple times if the user clicked the submit button rapidly.

### Solution: Submission Flag

Each form tracks submission state via `data-isSubmitting`:

```javascript
function ajaxFormSetup(formEl, modalEl) {
	formEl.dataset.ajaxWired = '1';
	formEl.dataset.isSubmitting = '0';

	formEl.addEventListener('submit', async function (e) {
		// Prevent duplicate submissions
		if (formEl.dataset.isSubmitting === '1') return;
		formEl.dataset.isSubmitting = '1';

		// ... fetch and handle response ...

		finally {
			formEl.dataset.isSubmitting = '0';
		}
	});
}
```

**Wired forms**:
- Create modal (`.createModal`)
- Edit modals (`#editModal-*`)
- Delete modals (`#deleteModal-*`)
- Comment reply forms (`#commentModal-*`, `#csCommentModal-*`, `#intCommentModal-*`)

---

## Notification Handling

### Defensive Checks

**Notifications are only sent if NotificationManager is available**:

```javascript
if (typeof NotificationManager === 'undefined') {
	console.log('[CsLiveHelp] NotificationManager not available; skipping notification');
	return;
}
```

### Notification Types

| Type | Condition | Message |
|------|-----------|---------|
| **RequestNotification** | New request created, escalated | `Brand - RequestType` |
| **CommentNotification** | Comment added to user's request | `Author commented on request #ID` |
| **MentionNotification** | User mentioned in internal comment | `Author mentioned you in request #ID` |

### Deduplication

Each notification is deduplicated using a key:
```javascript
const dedupeKey = `comment:${contextId}:${author}:${timestamp}`;
if (markSeen(dedupeKey)) return; // Skip if already sent
```

This prevents duplicate notifications if SignalR receives the same event twice.

### NotificationManager Call Safety

```javascript
const shouldNotify = !NotificationManager.isMuted?.(contextType, contextId);

if (shouldNotify) {
	try {
		NotificationManager.handleNotification({ /* ... */ });
	} catch (err) {
		console.warn('[CsLiveHelp] NotificationManager.handleNotification failed:', err);
	}
}
```

- Uses optional chaining (`?.`) to check if `isMuted` method exists
- Wraps the call in try/catch to prevent breakage if NotificationManager has issues

---

## Load-More Pagination

Each column with >50 cards shows a "Load more" button with:
- `data-status`: The column status (Open, InProgress, etc.)
- `data-after-id`: The lowest ID in the current set

Clicking loads the next 50 cards from `/CsLiveHelp/BoardPage?status=X&afterId=Y`:

```javascript
document.querySelectorAll('.load-more-btn').forEach(btn => {
	btn.addEventListener('click', async function () {
		const status = btn.dataset.status;
		const afterId = btn.dataset.afterId ?? '0';

		const res = await fetch(
			'/CsLiveHelp/BoardPage?status=' + encodeURIComponent(status) + '&afterId=' + afterId,
			{ headers: { 'X-Requested-With': 'XMLHttpRequest' } }
		);

		// Parse and insert new cards
		if (cards.length < 50) {
			btn.textContent = 'No more cards';
			btn.disabled = true;
		}
	});
});
```

---

## Drag-Drop Card Movement

### SortableJS Integration

Both Board and RequestsAllBrands use [SortableJS](https://sortablejs.com/) for drag-drop:

```javascript
document.querySelectorAll('.kanban-col').forEach(col => {
	Sortable.create(col, {
		group: 'kanban',
		animation: 150,
		ghostClass: 'sortable-ghost',
		onEnd: async function (evt) {
			const cardId = evt.item.dataset.cardId;
			const newStatus = evt.to.dataset.status;

			// POST to UpdateStatusJson
			const res = await fetch(`/CsLiveHelp/UpdateStatusJson/${cardId}`, {
				method: 'POST',
				body: new FormData().append('status', newStatus)
			});

			// Revert on failure
			if (!res.ok) evt.from.insertBefore(evt.item, evt.from.children[evt.oldIndex]);
		}
	});
});
```

**URLs**:
- Board/AM Requests: `/CsLiveHelp/UpdateStatusJson/{id}`
- Internal: `/CsLiveHelp/InternalUpdateStatusJson/{id}`

---

## Key JavaScript Files

### `wwwroot/js/cslivehelp.js` (Shared)
- **Size**: ~1000 lines
- **Loaded by**: Board.cshtml, Requests.cshtml, RequestsAllBrands.cshtml
- **Provides**:
  - SignalR connection setup
  - Event handlers (CardAdded, CardUpdated, CardStatusChanged, CardDeleted, CommentAdded)
  - Thread body detection
  - Modal lifecycle management
  - Form submission handling
  - Load-more pagination
  - Copy client ID functionality
  - Notification event handlers

### `wwwroot/js/cslivehelp-internal.js` (Internal Only)
- **Size**: ~300 lines
- **Loaded by**: RequestsAllBrands.cshtml only
- **Provides**:
  - **Overrides** shared CardStatusChanged to handle 4 columns (not 3)
  - **Overrides** CommentAdded to use `.int-comment-count` instead of `.comment-count`
  - Internal thread body detection
  - Drag-drop for internal board
  - Filter UI (brand + client ID)
  - Form submission for internal modals

---

## Testing Checklist

### Live Comment Updates
- [ ] Open Board.cshtml, open a comment modal
- [ ] Send a comment via another user/session
- [ ] Verify comment appears in the thread in real-time
- [ ] Verify comment count badge increments live
- [ ] Close and re-open modal → should show the new comment (fetched fresh for AM page)

### Modal Lifecycle
- [ ] Open a comment modal with existing comments
- [ ] Close the modal
- [ ] Re-open the same modal
- [ ] Verify no loading spinner appears
- [ ] Verify comments are visible (not stuck loading)

### Comment Count Updates
- [ ] On Board: Check `.comment-count` spans update live
- [ ] On Requests: Check `.comment-count` spans update live
- [ ] On Internal: Check `.int-comment-count` spans update live
- [ ] Add a comment via form submission → count updates immediately
- [ ] Verify comment appears in thread

### Notifications
- [ ] Create a new request on Board → AM receives RequestNotification
- [ ] Comment on a request → Relevant user receives CommentNotification
- [ ] Mention a user in internal comment → User receives MentionNotification
- [ ] Verify NotificationManager is called with correct payload
- [ ] Check deduplication (same notification not repeated)

### Form Submissions
- [ ] Rapidly click Submit button on a comment form
- [ ] Verify form only submits once
- [ ] Verify modal closes after successful submission
- [ ] Check for network errors → toast message shows, button re-enables

### Drag-Drop
- [ ] On Board/Internal: Drag a card to a different column
- [ ] Verify card moves immediately (optimistic UI)
- [ ] Verify status updates via POST
- [ ] On failure, card reverts to original column
- [ ] Column counts update in real-time

---

## Common Issues & Fixes

### Issue: Comment Modal Stuck on Loading
**Cause**: Modal-body still contains loading spinner after modal show event  
**Fix**: Ensure `show.bs.modal` handler fetches fresh content for AM Requests  
**Check**: Does `/CsLiveHelp/AmCommentThread/{id}` endpoint exist and return HTML?

### Issue: Comment Count Not Updating
**Cause**: Wrong selector (using `.comment-count` on internal page which uses `.int-comment-count`)  
**Fix**: `CommentAdded` handler now tries both selectors  
**Check**: Verify card HTML contains the correct class for the page

### Issue: Notifications Not Appearing
**Cause**: NotificationManager not loaded or undefined  
**Fix**: Added defensive checks (`typeof NotificationManager === 'undefined'`)  
**Check**: Is NotificationManager script loaded before cslivehelp.js?

### Issue: Modal Backdrops Stack Up
**Cause**: Rapid modal open/close without proper cleanup  
**Fix**: `cleanupStaleModalState()` called on every modal event  
**Check**: Does `document.querySelectorAll('.modal-backdrop').length` return to 0 after all modals close?

### Issue: Form Submits Twice
**Cause**: User clicks submit button rapidly  
**Fix**: Form submission flag `data-isSubmitting` prevents concurrent requests  
**Check**: Look at browser Network tab → only one POST request per form submission

---

## Performance Considerations

### Card Count Limits
- Cards are loaded in batches of 50
- Load-more buttons paginate when column has exactly 50 cards
- This prevents rendering thousands of DOM elements at once

### Comment Thread Scrolling
- Threads default to `max-height: 260px` with `overflow-y: auto`
- On new comment, auto-scroll to bottom: `threadBody.scrollTop = threadBody.scrollHeight`
- This keeps the newest comments visible without manual scrolling

### Modal Backdrop Cleanup
- Stale backdrops are removed to prevent visual glitches
- Checked on every modal event to ensure max 1 backdrop per open modal

### Deduplication
- Keeps last 500 notification keys in memory
- Prevents memory leak by removing oldest key when limit reached

---

## Related Documentation
- See `docs/CsLiveHelp-Architecture.md` for overall system design
- See Controller/Hub implementations for SignalR message routing
