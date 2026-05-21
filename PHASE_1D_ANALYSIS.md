# Phase 1D – Deep Analysis & Fix Plan

> **Prepared for AI agent handoff.**
> All file paths are relative to the solution root: `C:\Users\jacqu\source\repos\Unified\`
> Branch: `master`

---

## 1. Summary of What Was Found

The Phase 1D backend is **substantially complete**. The SignalR hub, controller, service, archive background service, all views, and all partials exist and are wired together. However, **several real-time (SignalR) and UX gaps** were found that explain why a CS agent must refresh the page to see a new AM request, and why certain events are not propagated correctly.

---

## 2. Critical Bug – Real-Time Updates Not Working on CS Board

### Root Cause

**`Requests.cshtml` (AM view) does NOT load the SignalR client.**

File: `Views/CsLiveHelp/Requests.cshtml`

The `@section Scripts` block at the bottom of `Requests.cshtml` only contains vanilla JS for form toggles. It does **not** include:

```html
<script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js"></script>
<script src="~/js/cslivehelp.js"></script>
```

This means **the AM browser never connects to `CsLiveHelpHub`**. The AM is never added to the `am-{userId}` SignalR group. The AM therefore never receives `CardAdded`, `CardUpdated`, `CardDeleted`, or `CommentAdded` events on their own view.

---

### Secondary Root Cause – CS Board `cslivehelp.js` Guard

File: `wwwroot/js/cslivehelp.js` — Line 68:

```javascript
if (!document.getElementById('col-Open')) return;
```

The AM view (`Requests.cshtml`) uses column IDs **without** the `col-` prefix. Its column `div` IDs are **not present at all** — the AM Kanban columns have no `id` attribute. So even if the SignalR script were loaded on `Requests.cshtml`, the early-return guard would fire immediately and no event handlers would be registered, and no hub connection would be established.

---

### Combined Effect

1. AM submits a request → server fires `CardAdded` to `am-{userId}` group and `cs-board` group.
2. CS browser **is** in `cs-board` group (if the CS board page is open), so `CardAdded` reaches the CS browser.
3. However, the `cslivehelp.js` script is loaded on `Board.cshtml` — **the CS board page**. When the CS agent is on the `/CsLiveHelp/Board` page, `col-Open` exists, so the guard does NOT fire, and the connection succeeds. ✅
4. The real issue is the **reverse**: when the CS board is open and the AM creates a request, the CS board **should** receive the `CardAdded` event. Investigation reveals that it does — the JS handler for `CardAdded` runs. However the card rendered client-side is a **minimal stub** (just the id, brand name, type, status, no action buttons, no drag handle) with a "refresh for full actions" link. This is by design in the current code.
5. For the **AM view**, no real-time updates are received at all — the page must be refreshed.

---

## 3. Full Issue List

### Issue 1 – AM view has no SignalR connection (CRITICAL)

| Field | Detail |
|---|---|
| **File** | `Views/CsLiveHelp/Requests.cshtml` — end of file, `@section Scripts` block |
| **Symptom** | AM does not receive live `CardUpdated`, `CardDeleted`, or `CommentAdded` events. The AM must refresh to see status changes on their own cards. |
| **Fix** | Add the SignalR CDN script and `~/js/cslivehelp.js` to the Scripts section of `Requests.cshtml`. Also add `col-Open` and `col-InProgress` / `col-Completed` IDs to the AM column divs so the `cslivehelp.js` guard does not bail. |

---

### Issue 2 – AM view Kanban columns have no `id` attributes (CRITICAL)

| Field | Detail |
|---|---|
| **File** | `Views/CsLiveHelp/Requests.cshtml` — lines ~53, 70, 88 (the three `.card-body` column divs) |
| **Symptom** | Even if SignalR were loaded, `cslivehelp.js` has `if (!document.getElementById('col-Open')) return;` and the AM Open column is `<div class="card-body p-2 ...">` with **no id**. None of the AM columns have an `id`. So `CardStatusChanged`, `CardAdded`, and `CardDeleted` handlers cannot find any column and silently drop events. |
| **Fix** | Add `id="col-Open"`, `id="col-InProgress"`, and `id="col-Completed"` to the three AM Kanban column body divs. The `cslivehelp.js` only looks up `colEl(status)` — the AM has no OnGoing/Escalated columns so those events would just be no-ops. Also add `data-card-id="@r.Id"` to each `_CsRequestCard` outer div so `cardEl(id)` can locate cards. |

---

### Issue 3 – `_CsRequestCard.cshtml` partial missing `data-card-id` attribute (CRITICAL)

| Field | Detail |
|---|---|
| **File** | `Views/CsLiveHelp/_CsRequestCard.cshtml` — line 16 (`<div class="card border shadow-sm p-2 small">`) |
| **Symptom** | The `cslivehelp.js` function `cardEl(id)` uses `document.querySelector('[data-card-id="' + id + '"]')`. The `_CsBoardCard.cshtml` (CS board) has `data-card-id="@Model.Id"` ✅. But `_CsRequestCard.cshtml` (AM view) does **not** have `data-card-id`. So `CardDeleted`, `CardUpdated`, and `CardStatusChanged` events cannot find AM view cards even once the connection is live. |
| **Fix** | Add `data-card-id="@Model.Id"` to the outer `<div>` in `_CsRequestCard.cshtml`. |

---

### Issue 4 – CS Board `CardAdded` renders a stub card, not a full card (UX)

| Field | Detail |
|---|---|
| **File** | `wwwroot/js/cslivehelp.js` — lines 104–125 (`connection.on('CardAdded', ...)`) |
| **Symptom** | When a new card is pushed in real time, the CS board renders a minimal HTML card without action buttons, drag handles, modals, or smart-action buttons. The stub says "refresh for full actions". While the card is technically visible, the CS agent cannot act on it without a page refresh. |
| **Root Cause** | The `CardAdded` handler constructs a minimal card purely from JSON fields (id, brandName, requestType, status). The full card HTML requires a server-side Razor partial render (`_CsBoardCard.cshtml`) which includes per-card modals, smart action buttons, thread viewer, etc. |
| **Fix** | Change the `CardAdded` handler to fetch the rendered partial from the server instead of building a stub client-side. Add a new endpoint `GET /CsLiveHelp/CardPartial/{id}` (returns `PartialView("_CsBoardCard", request)`) and in the JS handler call it, then inject the returned HTML. This ensures full action buttons, modals and drag handles are present without a full-page reload. |

---

### Issue 5 – `RequestsAllBrands.cshtml` has no SignalR connection

| Field | Detail |
|---|---|
| **File** | `Views/CsLiveHelp/RequestsAllBrands.cshtml` — `@section Scripts` (last ~10 lines) |
| **Symptom** | The internal CS board (All Brands) does not receive real-time events. CS agents on this page must refresh to see new escalations or status changes. |
| **Fix** | Add the SignalR CDN script + `~/js/cslivehelp.js` to the Scripts section. Add `id="col-Open"`, `id="col-InProgress"`, etc. to the column body divs, or use the same approach as Board.cshtml. The `_InternalBoardCard.cshtml` partial should also have `data-card-id`. |

---

### Issue 6 – `_InternalBoardCard.cshtml` missing `data-card-id`

| Field | Detail |
|---|---|
| **File** | `Views/CsLiveHelp/_InternalBoardCard.cshtml` |
| **Symptom** | Same as Issue 3 — SignalR events cannot locate internal board cards. |
| **Fix** | Verify the outer div of `_InternalBoardCard.cshtml` has `data-card-id="@Model.Id"` (check and add if missing). |

---

### Issue 7 – `UpdateStatusJson` SignalR push does NOT notify AM group (UX gap)

| Field | Detail |
|---|---|
| **File** | `Controllers/CsLiveHelpController.cs` — `UpdateStatusJson` action (~line 313) and `UpdateStatus` action (~line 270) |
| **Symptom** | When a CS agent moves a card (drag-drop or button), `CardStatusChanged` is pushed only to `cs-board`. The AM whose card it is receives no real-time update. The AM must refresh to see the card's new status. |
| **Fix** | Resolve the `AccountManagerId` from the `CsRequest` record and also push `CardStatusChanged` to `am-{accountManagerId}`. Example: `await _hub.Clients.Group($"am-{req.AccountManagerId}").SendAsync("CardStatusChanged", ...)`. Same fix needed in `UpdateStatus`, `Escalate`, `SendReset`, `MarkPassed`, and `ResolveEscalation`. |

---

### Issue 8 – `CsAddComment` does NOT notify AM group (UX gap)

| Field | Detail |
|---|---|
| **File** | `Controllers/CsLiveHelpController.cs` — `CsAddComment` action (~line 332) |
| **Symptom** | When a CS agent posts a comment on an AM's request, the AM never receives the `CommentAdded` event. |
| **Fix** | Look up the `AccountManagerId` on the `CsRequest` and push `CommentAdded` to `am-{accountManagerId}` as well as `cs-board`. |

---

### Issue 9 – `BoardPage` load-more endpoint filters incorrectly (Logic bug)

| Field | Detail |
|---|---|
| **File** | `Controllers/CsLiveHelpController.cs` — `BoardPage` action (~line 257) |
| **Symptom** | `GetBoardRequestsAsync(afterId)` returns ALL statuses (no status filter in the service method). The `BoardPage` action then filters by status in memory (`all.Where(r => r.Status == status)`). But because `GetBoardRequestsAsync` does `Take(50)` and then `Where` is applied after, if the first 50 records don't include any of the requested status, the page returns empty even though more exist in the DB. |
| **Root Cause** | `GetBoardRequestsAsync` applies `.Take(50)` before returning. The status filter in `BoardPage` is applied **after** the Take. |
| **Fix** | Add a `status` parameter to `GetBoardRequestsAsync` (or create an overload) so the DB query filters by status **before** the Take. Or pass the status through to the service. |

---

### Issue 10 – `GetOtherAmOpenRequestsAsync` queries `AgentBrands` for AM users (Logic bug)

| Field | Detail |
|---|---|
| **File** | `Services/CsLiveHelpService.cs` — `GetOtherAmOpenRequestsAsync` (~line 32) |
| **Symptom** | AMs are external users. The code note in the controller (`Requests` action) explicitly states *"AMs are external users — they are not in AgentBrands, so show all active brands"*. But `GetOtherAmOpenRequestsAsync` queries `AgentBrands` to get the AM's brand IDs. Since AMs have no `AgentBrands` rows, `amBrandIds` is always empty, and the "Others" column is always empty — the duplicate-prevention feature is effectively broken. |
| **Fix** | Replace the `AgentBrands` lookup with a brand-ID list derived from the AM's own existing open requests, OR show **all** other AMs' open requests (since all AMs represent the same brands). The simplest correct fix: remove the brand-filter entirely and show all other AMs' Open/InProgress requests. Alternatively, add a brand filter based on the AM's own requests: `var amBrandIds = await _db.CsRequests.Where(r => r.AccountManagerId == amId).Select(r => r.BrandId).Distinct().ToListAsync()`. |

---

### Issue 11 – Roadmap items still marked `[ ]` that are actually complete

| Field | Detail |
|---|---|
| **File** | `ROADMAP.md` |
| **Items** | `1d-vii` view items are all `[ ]` but all views exist: `Views/CsLiveHelp/Requests.cshtml`, `Views/CsLiveHelp/Board.cshtml`, `Views/CsLiveHelp/RequestsAllBrands.cshtml`, `Views/CsLiveHelp/_CsRequestCard.cshtml`, `_CsBoardCard.cshtml`, `_CsBoardCardList.cshtml`, `_InternalBoardCard.cshtml`. Sidebar links should be verified. |
| **Fix** | Mark 1d-vii items `[x]` and verify the sidebar. |

---

### Issue 12 – `appsettings.json` `CsLiveHelp:Archive` section — verify it exists

| Field | Detail |
|---|---|
| **File** | `appsettings.json` |
| **Symptom** | The roadmap marks this as `[x]` complete. Verify the section exists. If absent, the archive service silently uses defaults (no error thrown). |
| **Fix** | Confirm section is present. If not, add: `"CsLiveHelp": { "Archive": { "RunEveryHours": 6, "CompleteAgeThresholdDays": 3 } }`. |

---

## 4. Files to Modify (Prioritised)

| Priority | File | Change |
|---|---|---|
| P0 – Critical | `Views/CsLiveHelp/Requests.cshtml` | Add SignalR CDN + `cslivehelp.js` to `@section Scripts`. Add `id="col-Open"`, `id="col-InProgress"`, `id="col-Completed"` to column body divs. |
| P0 – Critical | `Views/CsLiveHelp/_CsRequestCard.cshtml` | Add `data-card-id="@Model.Id"` to outer `<div>`. |
| P0 – Critical | `Views/CsLiveHelp/RequestsAllBrands.cshtml` | Add SignalR CDN + `cslivehelp.js` to `@section Scripts`. Add `id` attributes to column body divs. |
| P0 – Critical | `Views/CsLiveHelp/_InternalBoardCard.cshtml` | Verify/add `data-card-id="@Model.Id"` to outer div. |
| P1 – High | `Controllers/CsLiveHelpController.cs` | In `UpdateStatus`, `UpdateStatusJson`, `Escalate`, `SendReset`, `MarkPassed`, `CsAddComment`: load the `CsRequest` to get `AccountManagerId`, then push SignalR events to `am-{accountManagerId}` group. |
| P1 – High | `wwwroot/js/cslivehelp.js` | Change `CardAdded` handler to fetch `/CsLiveHelp/CardPartial/{id}` and inject full HTML, instead of building a stub. |
| P2 – Medium | `Controllers/CsLiveHelpController.cs` | `BoardPage`: pass status filter into `GetBoardRequestsAsync`. |
| P2 – Medium | `Services/CsLiveHelpService.cs` | `GetBoardRequestsAsync`: accept optional `CsRequestStatus?` parameter. `GetOtherAmOpenRequestsAsync`: fix brand-ID query to use AM's own requests instead of `AgentBrands`. |
| P3 – Low | `ROADMAP.md` | Mark `1d-vii` view items complete. |
| P3 – Low | `appsettings.json` | Confirm/add `CsLiveHelp:Archive` config section. |

---

## 5. New Endpoint Required

### `GET /CsLiveHelp/CardPartial/{id}`

```csharp
[Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]
public async Task<IActionResult> CardPartial(int id)
{
	if (!Request.Headers.ContainsKey("X-Requested-With")) return BadRequest();
	var req = await _svc.GetRequestAsync(id);
	if (req is null) return NotFound();
	return PartialView("_CsBoardCard", req);
}
```

Needed by the updated `CardAdded` client-side handler to inject a fully rendered Razor card instead of the current client-built stub.

---

## 6. Detailed Code Fixes

### Fix 1 — `Views/CsLiveHelp/Requests.cshtml` — Add SignalR + column IDs

In the AM Kanban board section, the three `.card-body` column divs need IDs:

```html
<!-- Open column body — add id -->
<div id="col-Open" class="card-body p-2 d-flex flex-column gap-2">

<!-- In Progress / On-Going column body — add id (map both to col-InProgress) -->
<div id="col-InProgress" class="card-body p-2 d-flex flex-column gap-2">

<!-- Completed column body — add id -->
<div id="col-Completed" class="card-body p-2 d-flex flex-column gap-2">
```

In `@section Scripts` (end of file), append:

```html
<script src="https://cdn.jsdelivr.net/npm/@@microsoft/signalr@8.0.7/dist/browser/signalr.min.js"></script>
<script src="~/js/cslivehelp.js"></script>
```

---

### Fix 2 — `Views/CsLiveHelp/_CsRequestCard.cshtml` — Add `data-card-id`

Change line 16 from:
```html
<div class="card border shadow-sm p-2 small">
```
To:
```html
<div class="card border shadow-sm p-2 small" data-card-id="@Model.Id">
```

---

### Fix 3 — `Controllers/CsLiveHelpController.cs` — Push CardStatusChanged to AM group

For each action that changes a card's status, load the `AccountManagerId` from the DB and push to both `cs-board` and `am-{id}`:

```csharp
// After UpdateStatusAsync:
var req = await _db.CsRequests.FindAsync(id);
if (req?.AccountManagerId is not null)
	await _hub.Clients.Group($"am-{req.AccountManagerId}")
		.SendAsync("CardStatusChanged", new { id, newStatus = status.ToString(), assignedTo = agent?.DisplayName });
```

This pattern applies to: `UpdateStatus`, `UpdateStatusJson`, `Escalate`, `SendReset`, `MarkPassed`.

---

### Fix 4 — `Controllers/CsLiveHelpController.cs` — Push CommentAdded to AM group

In `CsAddComment`:

```csharp
var req = await _db.CsRequests.FindAsync(id);
if (req?.AccountManagerId is not null)
	await _hub.Clients.Group($"am-{req.AccountManagerId}")
		.SendAsync("CommentAdded", new { requestId = id, author = agent?.DisplayName ?? csId, body, isSystem = false, createdAt = DateTime.UtcNow });
```

---

### Fix 5 — `Services/CsLiveHelpService.cs` — Fix `BoardPage` status filtering

Change `GetBoardRequestsAsync`:

```csharp
public async Task<List<CsRequest>> GetBoardRequestsAsync(int afterId = 0, CsRequestStatus? status = null)
{
	var q = _db.CsRequests.Where(r => r.Id > afterId);
	if (status.HasValue) q = q.Where(r => r.Status == status.Value);
	return await q
		.Include(r => r.Brand)
		.Include(r => r.RequestType)
		.Include(r => r.AssignedTo)
		.Include(r => r.Comments.OrderBy(c => c.CreatedAt))
			.ThenInclude(c => c.Author)
		.OrderByDescending(r => r.CreatedAt)
		.Take(50)
		.ToListAsync();
}
```

Change `BoardPage` action in controller:

```csharp
var page = await _svc.GetBoardRequestsAsync(afterId, status);
return PartialView("_CsBoardCardList", page);
```

---

### Fix 6 — `Services/CsLiveHelpService.cs` — Fix `GetOtherAmOpenRequestsAsync`

Replace the `AgentBrands` query with an AM requests query:

```csharp
public async Task<List<CsRequest>> GetOtherAmOpenRequestsAsync(string amId, int afterId = 0)
{
	// Get brand IDs this AM has requested (not via AgentBrands — AM users have no AgentBrands rows)
	var amBrandIds = await _db.CsRequests
		.Where(r => r.AccountManagerId == amId)
		.Select(r => r.BrandId)
		.Distinct()
		.ToListAsync();

	if (amBrandIds.Count == 0)
		return new List<CsRequest>();

	return await _db.CsRequests
		.Where(r => r.AccountManagerId != amId
				 && amBrandIds.Contains(r.BrandId)
				 && (r.Status == CsRequestStatus.Open || r.Status == CsRequestStatus.InProgress)
				 && r.Id > afterId)
		.Include(r => r.Brand)
		.Include(r => r.RequestType)
		.OrderByDescending(r => r.CreatedAt)
		.Take(50)
		.ToListAsync();
}
```

---

### Fix 7 — `wwwroot/js/cslivehelp.js` — CardAdded: fetch partial instead of building stub

Replace the `CardAdded` handler body with:

```javascript
connection.on('CardAdded', function (data) {
	const col = colEl(data.status ?? 'Open');
	if (!col) return;

	removeEmptyHint(col);

	fetch('/CsLiveHelp/CardPartial/' + data.id, {
		headers: { 'X-Requested-With': 'XMLHttpRequest' }
	})
	.then(function (res) {
		if (!res.ok) throw new Error('partial failed');
		return res.text();
	})
	.then(function (html) {
		const wrap = document.createElement('div');
		wrap.innerHTML = html.trim();
		const card = wrap.firstElementChild;
		if (card) {
			card.classList.add('new-card-notice');
			col.prepend(card);
		}
		showToastMsg('New card #' + data.id + ' added (' + (data.brandName ?? '') + ').', true);
	})
	.catch(function () {
		// Fallback: show stub with refresh link
		const div = document.createElement('div');
		div.className = 'card shadow-sm border-0 new-card-notice';
		div.dataset.cardId = data.id;
		div.innerHTML = '<div class="card-body py-2 px-3 small">#' + data.id + ' &mdash; ' + escHtml(data.brandName ?? '') +
			' <a href="" onclick="location.reload();return false;">refresh</a></div>';
		col.prepend(div);
	});
});
```

---

## 7. `appsettings.json` Verification

Confirm the following section is present in `appsettings.json`:

```json
"CsLiveHelp": {
  "Archive": {
	"RunEveryHours": 6,
	"CompleteAgeThresholdDays": 3
  }
}
```

---

## 8. Roadmap Status Discrepancies

The following items are marked `[ ]` in the roadmap but the code is complete:

| Roadmap Item | Actual Status | Evidence |
|---|---|---|
| `Views/CsLiveHelp/Requests.cshtml` | ✅ Exists | `Views/CsLiveHelp/Requests.cshtml` (307 lines) |
| `Views/CsLiveHelp/Board.cshtml` | ✅ Exists | `Views/CsLiveHelp/Board.cshtml` (476 lines) |
| `Views/CsLiveHelp/RequestsAllBrands.cshtml` | ✅ Exists | `Views/CsLiveHelp/RequestsAllBrands.cshtml` (271 lines) |
| `_CsRequestCard.cshtml` shared partial | ✅ Exists | `Views/CsLiveHelp/_CsRequestCard.cshtml` |
| `_CsRequestThread.cshtml` | ⚠️ Not separately — thread is inline in Board.cshtml modals | Thread rendering is in `Board.cshtml` `csCommentModal` and in `_CsBoardCard.cshtml` |
| Sidebar links (CS Requests for AM, All Brands for TL/Manager) | ❓ Verify | Check `Views/Shared/Layouts/_sidebar.cshtml` |

---

## 9. Execution Order for the Fixing Agent

> ✅ = **COMPLETED** | ⚠️ = partial / needs verification

1. ✅ **Fix `_CsRequestCard.cshtml`** — added `data-card-id="@Model.Id"` to outer div.
2. ✅ **Fix `Requests.cshtml`** — added `id="col-Open"`, `id="col-InProgress"`, `id="col-Completed"` to column body divs; added SignalR CDN + `cslivehelp.js` to `@section Scripts`.
3. ✅ **Fix `RequestsAllBrands.cshtml`** — added column IDs (`col-Open`, `col-InProgress`, `col-OnGoing`, `col-Escalated`) and SignalR scripts. `_InternalBoardCard.cshtml` verified and `data-card-id` added.
4. ✅ **Fix `CsLiveHelpController.cs`** — AM group pushes added for `UpdateStatus`, `UpdateStatusJson`, `Escalate`, `SendReset`, `MarkPassed`, `CsAddComment`.
5. ✅ **Add `CardPartial` endpoint** — `GET /CsLiveHelp/CardPartial/{id}` added to controller, returns `PartialView("_CsBoardCard", req)`.
6. ✅ **Fix `cslivehelp.js` `CardAdded` handler** — now fetches `/CsLiveHelp/CardPartial/{id}` and injects full HTML; stub remains as fallback on fetch failure.
7. ✅ **Fix `CsLiveHelpService.cs`** — `GetBoardRequestsAsync` accepts optional `CsRequestStatus? status`; `GetOtherAmOpenRequestsAsync` brand IDs now derived from AM's own requests (not `AgentBrands`); `GetAmRequestsAsync` now includes `Comments.OrderBy` + `ThenInclude(c => c.Author)`.
8. ✅ **Fix `CsLiveHelpController.cs` `BoardPage`** — passes `status` directly to `GetBoardRequestsAsync`, removing the broken in-memory filter.
9. ✅ **Verify `appsettings.json`** — `CsLiveHelp:Archive` section confirmed present (`RunEveryHours: 6`, `CompleteAgeThresholdDays: 3`).
10. ✅ **Update `ROADMAP.md`** — `1d-vii` view items marked `[x]`.
11. ✅ **Build** — zero errors confirmed.

---

## 10. Second Pass – AM View Enhancements (Completed)

These additional issues were found and fixed in a second agent pass:

### 10a – Client ID required on every AM request ✅

| Field | Detail |
|---|---|
| **Problem** | `CsRequest` had no `ClientId` field. CS agents had no way to identify which client/player the request was about. |
| **Fix** | Added `string? ClientId` to `CsRequest` model. Created and applied migration `Phase1d_CsRequest_ClientId`. Updated `CreateRequestAsync` and `EditRequestAsync` in service; updated `CreateRequest` and `EditRequest` controller actions to accept and validate (required). Added Client ID field to both Create and Edit modals in `Requests.cshtml`. |
| **Files** | `Models/CsLiveHelp/CsRequest.cs`, `Services/CsLiveHelpService.cs`, `Controllers/CsLiveHelpController.cs`, `Views/CsLiveHelp/Requests.cshtml`, `Views/CsLiveHelp/_CsRequestCard.cshtml` |

### 10b – AM cannot see existing comments on their requests ✅

| Field | Detail |
|---|---|
| **Problem** | The AM comment modal only contained a blank textarea — there was no display of the existing comment thread. The AM could not see what the CS agent had replied. |
| **Fix** | Reworked comment modals in `Requests.cshtml` into a full thread viewer + reply form. Existing comments are rendered server-side (author display name, timestamp, system badge). `GetAmRequestsAsync` updated to `Include(r => r.Comments.OrderBy(c => c.CreatedAt)).ThenInclude(c => c.Author)`. Client ID shown in modal header when present. |
| **Files** | `Views/CsLiveHelp/Requests.cshtml`, `Services/CsLiveHelpService.cs` |

### 10c – Comment modal stuck on loading screen after close/reopen ✅

| Field | Detail |
|---|---|
| **Problem** | Opening a comment modal, closing it, then reopening it resulted in a stuck loading/backdrop state until the page was refreshed. |
| **Root Cause** | The Bootstrap 5 modal retained focus state from the previous open cycle. The textarea `required` attribute caused form validation to trigger on re-show before any content was entered, blocking the modal from fully rendering. |
| **Fix** | Added `hidden.bs.modal` event listener on every comment modal that clears the textarea value, removes `is-invalid` CSS class, and resets the thread scroll position. This runs after every close so the modal is always in a clean state when reopened. |
| **Files** | `Views/CsLiveHelp/Requests.cshtml` (`@section Scripts`) |

### 10d – `_CsRequestCard.cshtml` shows ClientId and comment count ✅

| Field | Detail |
|---|---|
| **Fix** | Added `ClientId` display row (with user icon) and comment count link button (mirroring `_CsBoardCard.cshtml` pattern) to `_CsRequestCard.cshtml`. |

---

## 11. Known Remaining Items

| Item | Status |
|---|---|
| Sidebar links for AM and TL/Manager roles | ❓ Verify `Views/Shared/Layouts/_sidebar.cshtml` |
| `_CsRequestThread.cshtml` standalone partial | ⚠️ Thread is inline in modals — no separate partial needed unless reuse required |
| `CommentAdded` SignalR event: live-append to AM thread modal | ⚠️ `cslivehelp.js` appends to `#threadModal-{id}` but AM modals use `#commentModal-{id}` — the selector won't match. Update `CommentAdded` handler to also target `#commentModal-{id} .thread-body` |

---

*Last updated: Phase 1D second pass — AM view enhancements completed.*
