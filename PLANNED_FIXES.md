# Planned Fixes & Improvements — Unified CS Live Help

> Last updated: 2025-06 | Branch: `master`

---

## ✅ Completed

### Comment isolation between CS-internal board and AM board
- `RequestsAllBrands.cshtml` comment threads are now flagged `IsCsInternalOnly = true`.
- AM-facing views (`Requests.cshtml`, `Board.cshtml`) never surface CS-internal notes — no count, no badge, no hint.
- `_CsRequestCard` (AM) only counts/shows `IsCsInternalOnly = false` comments.
- `_InternalBoardCard` comment count is clickable and opens the CS-internal thread modal.

### AM Requests page — live updates (no full-page refreshes)
- Create, Edit, Delete, and AddComment forms in `Requests.cshtml` now submit via `fetch` (AJAX).
- Success/error feedback via toast notification; DOM is updated by existing SignalR events.
- Escalated cards correctly land in the **In Progress** column on the AM board (not Open).

---

## 🔧 Pending / Backlog

### General
- [ ] Add pagination / "Load more" to `Requests.cshtml` AM board (currently loads all own requests).
- [ ] Add filter bar (by brand, by status) to `RequestsAllBrands.cshtml`.
- [ ] Archive old completed requests automatically after N days (configurable).

### CS Board (`Board.cshtml`)
- [ ] Drag-drop revert animation should be smoother when a move is rejected by the server.
- [ ] Show timestamp of last status change on each card.
- [ ] Add bulk-complete action for TL/Manager on the Completed column.

### AM Requests (`Requests.cshtml`)
- [ ] Real-time push new comments into open thread modal without closing and re-opening.
- [ ] Show typing indicator when CS agent is composing a reply.
- [ ] Email notification to AM when CS posts a comment on their request.

### RequestsAllBrands (`RequestsAllBrands.cshtml`)
- [ ] TL/Manager: add summary panel showing count per brand / per type.
- [ ] Export visible cards to CSV.
- [ ] "Assign to agent" dropdown per card (currently auto-assigns on status move).

### Infrastructure
- [ ] Add integration tests for `CsLiveHelpController` AM and CS paths.
- [ ] Rate-limit CS agent comment submissions (currently only AM is rate-limited).
- [ ] Add SignalR group for `RequestsAllBrands` page so CS-internal comment events don't bleed to `am-*` groups.

---

## 🐛 Known Issues

| # | Area | Description | Severity |
|---|------|-------------|----------|
| 1 | `Board.cshtml` | `CsAddComment` was incorrectly flagging comments as `IsCsInternalOnly=true`; fixed in this session. | High |
| 2 | `Requests.cshtml` | Escalated cards appeared in the Open column due to incorrect server-side grouping; fixed. | Medium |
| 3 | `_InternalBoardCard` | Comment count was plain text, not clickable; fixed to open the thread modal. | Low |
