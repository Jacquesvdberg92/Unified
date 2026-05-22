# CS Live Help – Architecture Reference

> **Purpose:** This document explains the three core CS Live Help pages, how they relate to each other, the roles that can access each page, and the expected SignalR real-time behavior between them. It is intended as a quick-start guide for any future agent or developer working on this feature.

---

## Pages Overview

| Page | View file | Route | Roles |
|---|---|---|---|
| AM Requests | `Views/CsLiveHelp/Requests.cshtml` | `GET /CsLiveHelp/Requests` | `AccountManager` |
| CS Board | `Views/CsLiveHelp/Board.cshtml` | `GET /CsLiveHelp/Board` | `CSAgent`, `TeamLeader`, `BrandManager`, `SwissArmyKnife` |
| All Brands | `Views/CsLiveHelp/RequestsAllBrands.cshtml` | `GET /CsLiveHelp/RequestsAllBrands` | `CSAgent`, `TeamLeader`, `BrandManager`, `SwissArmyKnife` |

---

## Page-by-Page Details

### 1. `Requests.cshtml` — Account Manager View
- Shows **only the AM's own requests** in three kanban columns: `Open`, `In Progress / Escalated`, `Completed`.
- A read-only fourth column shows **other AMs' open requests** for shared brands (no actions allowed).
- The AM can: create, edit, delete (own Open requests), and reply to their own comment threads.
- **AMs can never see CS-internal (`IsCsInternalOnly`) comments.**
- Uses card partial `_CsRequestCard.cshtml`.
- Per-card comment modals are `#commentModal-{id}`.
- Card/modal HTML is fetched live via `GET /CsLiveHelp/AmCardPartial/{id}` and `GET /CsLiveHelp/AmCardModalsPartial/{id}`.
- Both endpoints require `AccountManager` role and validate `req.AccountManagerId == amId`.

### 2. `Board.cshtml` — CS Agent Live Board
- Shows **all active requests** (not Completed by default) across five kanban columns: `Open`, `InProgress`, `OnGoing`, `Escalated`, `Completed`.
- CS agents can: drag-drop cards to change status, add public comments (`CsAddComment`), escalate (`Escalate`), send password resets (`SendReset`), mark passed (`MarkPassed`).
- Uses card partial `_CsBoardCard.cshtml`.
- Per-card modals: `#statusModal-{id}`, `#csCommentModal-{id}`, `#escalateModal-{id}`, `#resetModal-{id}`, `#passedModal-{id}`.
- Card HTML is fetched live via `GET /CsLiveHelp/CardPartial/{id}` (CS-role-only).
- Modal HTML is fetched live via `GET /CsLiveHelp/CardModalsPartial/{id}` (CS-role-only).
- Includes a simulation panel (TeamLeader / BrandManager / SwissArmyKnife only) driven by `SimulationStep` SignalR events.

### 3. `RequestsAllBrands.cshtml` — CS Internal All-Brands Board
- Shows **all internal requests** (`IsInternal == true`) **plus escalated AM-originated requests** (`Status == Escalated`) across four columns: `Open`, `InProgress`, `OnGoing`, `Completed`.
- CS agents can: drag-drop cards, move via status modal, post CS-internal comments (`InternalAddComment`).
- **Internal comments (`IsCsInternalOnly = true`) are private to CS — they are never pushed to AM SignalR groups and are never shown in `Requests.cshtml`.**
- Uses card partial `_InternalBoardCard.cshtml`.
- Per-card modals: `#intStatusModal-{id}`, `#intCommentModal-{id}`, `#intResolveModal-{id}` (TL/Manager only for Escalated cards).
- `InternalAddComment` accepts: images (jpg/png/gif/webp ≤ 5 MB) **and** documents (pdf/doc/docx/xls/xlsx ≤ 20 MB). Images are stored under `/uploads/cs-comments/{id}/`; documents under `/uploads/cs-docs/{id}/`.
- **Team Leaders, Brand Managers, and SwissArmyKnife can resolve escalations** via `POST /CsLiveHelp/ResolveEscalation/{id}`.

---

## Data Model Summary

```
CsRequest
  ├── AccountManagerId  (null for internal requests)
  ├── AccountManager    (navigation to AppUser)
  ├── IsInternal        (true for requests created from All Brands page)
  ├── Status            (Open | InProgress | OnGoing | Escalated | Completed)
  ├── Brand, RequestType, ClientId, CustomDescription
  ├── AssignedToId      (the CS agent who last moved this card)
  ├── AssignedTo        (navigation to AppUser — the mover)
  └── Comments[]
		├── IsCsInternalOnly  (true = only visible to CS roles)
		└── IsSystemMessage   (true = auto-posted by escalate/resolve/reset actions)

CsRequestComment
  ├── RequestId
  ├── AuthorId
  ├── Author            (navigation to AppUser)
  ├── Body              (comment text, max 1000 chars)
  ├── ImagePath         (jpg/png/gif/webp ≤ 5 MB, stored at /uploads/cs-comments/{id}/)
  ├── DocumentPath      (pdf/doc/docx/xls/xlsx ≤ 20 MB, stored at /uploads/cs-docs/{id}/ — All Brands only)
  ├── IsCsInternalOnly  (true = comment only visible in All Brands, never sent to AM)
  ├── IsSystemMessage   (true = system-generated message)
  └── CreatedAt
```

**Visibility rules:**
- `Requests.cshtml` queries `GetAmRequestsAsync(amId)` → own requests only, comments filtered to `!IsCsInternalOnly`.
- `Board.cshtml` queries `GetBoardRequestsAsync()` → all non-Completed, non-Internal requests; all comments visible (CS public ones).
- `RequestsAllBrands.cshtml` queries `GetAllBrandsRequestsAsync()` → `IsInternal || Status == Escalated`; all comments visible (CS roles see internal notes too).

---

## SignalR Architecture

### Hub: `CsLiveHelpHub` (`/hubs/cslivehelp`)

**Group membership (set in `OnConnectedAsync`):**
- `AccountManager` role → joins group `am-{userId}` (receives only their own card updates)
- CS roles (`CSAgent`, `TeamLeader`, `BrandManager`, `SwissArmyKnife`) → joins group `cs-board` (receives all public updates)

**Shared client: `wwwroot/js/cslivehelp.js`**
All three pages load the same SignalR client. Page-specific behavior is controlled by:
- `window.csCardPartialUrl` — URL prefix for live card partial fetches (defaults to `/CsLiveHelp/CardPartial/` for CS Board)
  - `Requests.cshtml` sets: `/CsLiveHelp/AmCardPartial/` (AM-only endpoint)
  - `RequestsAllBrands.cshtml` sets: `/CsLiveHelp/InternalCardPartial/` (internal board endpoint)
  - `Board.cshtml` uses default (CS Board endpoint)
- `window.csModalsPartialUrl` — URL prefix for live modal partial fetches (defaults to `/CsLiveHelp/CardModalsPartial/` for CS Board)
  - `Requests.cshtml` sets: `/CsLiveHelp/AmCardModalsPartial/` (AM comment modal endpoint)

### Events emitted by the controller

| Controller action | SignalR event | Groups notified |
|---|---|---|
| `CreateRequest` (AM creates) | `CardAdded` | `am-{amId}`, `cs-board` |
| `AddComment` (AM replies) | `CommentAdded` | `am-{amId}`, `cs-board` |
| `CsAddComment` (CS replies, public) | `CommentAdded` | `am-{amId}`, `cs-board` |
| `InternalAddComment` (CS-internal note) | `CommentAdded` | `cs-board` **only** |
| `UpdateStatus` / `UpdateStatusJson` (CS drag-drop) | `CardStatusChanged` | `am-{amId}`, `cs-board` |
| `InternalUpdateStatus` / `InternalUpdateStatusJson` | `CardStatusChanged` | `cs-board` **only** |
| `Escalate` | `CardStatusChanged` (→ `Escalated`) | `am-{amId}`, `cs-board` |
| `ResolveEscalation` | `CardStatusChanged` (→ `Completed`) | `am-{amId}` (if set), `cs-board` |
| `CardAdded` (internal request) | `CardAdded` | `cs-board` **only** |
| `CardDeleted` | `CardDeleted` | `am-{amId}`, `cs-board` |

### Events consumed by `cslivehelp.js`

| Event | Handler behavior |
|---|---|
| `CardAdded` | Fetches card partial, prepends to correct column, calls `ensureCardModals` then `wireCommentModal` |
| `CardUpdated` | Highlights the existing card with a border flash |
| `CardStatusChanged` | Moves card between `#col-{status}` columns, updates badge class/text; unmapped statuses fall back to `InProgress` on AM page; updates `.cs-assigned-name` if `assignedTo` provided |
| `CardDeleted` | Removes card and associated modals from DOM |
| `CommentAdded` | Increments comment count badge on card; appends comment HTML to `#threadModal-N .thread-body`, `#commentModal-N .thread-body`, `#csCommentModal-N .thread-body`, or `#intCommentModal-N .modal-body .border.rounded`; upgrades the "No comments yet" placeholder if needed; AM modals refresh thread on next open via `GET /CsLiveHelp/AmCommentThread/{id}` |

---

## Key Invariants (must remain true)

1. **CS-internal comments never reach AMs.** `InternalAddComment` only pushes to `cs-board`. `Requests.cshtml` filters `Comments` on `!IsCsInternalOnly`. The `AmCommentThread` endpoint also filters `!IsCsInternalOnly`.
2. **AM-originated cards that get escalated appear in All Brands.** `GetAllBrandsRequestsAsync` includes rows where `Status == Escalated` regardless of `IsInternal`.
3. **Resolving an escalation notifies the AM.** `ResolveEscalation` emits `CardStatusChanged(Completed)` to both `cs-board` and `am-{AccountManagerId}`.
4. **Card partials are role-gated.** CS uses `/CardPartial/` and `/InternalCardPartial/`; AMs use `/AmCardPartial/` and `/AmCardModalsPartial/`. All endpoints validate ownership/role before returning HTML. The `AmCommentThread` endpoint validates that the request belongs to the logged-in AM.
5. **New dynamically injected modals are wired for AJAX submission.** After `ensureCardModals` resolves, `wireCommentModal(id)` is called so the reply form submits via `ajaxFormSetup` without a page reload.
6. **File attachments on All Brands page support images and office documents.** Images (≤ 5 MB) go to `/uploads/cs-comments/{id}/`; documents (≤ 20 MB) go to `/uploads/cs-docs/{id}/`. Thread rendering checks the extension and renders an inline preview for images or a download-link button for documents.
7. **AM requests are rate-limited.** `CreateRequest` uses `IsRateLimitedAsync(amId)` to prevent submission spam. AMs who try to exceed the limit get an error message in both AJAX and form submission flows.
8. **Card assignment persists drag-drop mover.** When a CS agent drags a card to a new status, `UpdateStatusJson` calls `UpdateStatusAsync(id, status, csId)` which sets `AssignedToId = csId` and broadcasts `CardStatusChanged` with `assignedTo = agent?.DisplayName`.

---

## Controller Endpoints Reference

### Partial Endpoints (AJAX-only, return HTML fragments)

| Endpoint | Method | Role | Purpose |
|---|---|---|---|
| `GET /CsLiveHelp/CardPartial/{id}` | GET | CS roles | Fetch card HTML for CS Board (live updates) |
| `GET /CsLiveHelp/CardModalsPartial/{id}` | GET | CS roles | Fetch modal markup for CS Board card |
| `GET /CsLiveHelp/InternalCardPartial/{id}` | GET | CS roles | Fetch card HTML for All Brands board (internal) |
| `GET /CsLiveHelp/AmCardPartial/{id}` | GET | AccountManager | Fetch card HTML for AM Requests page (validates ownership) |
| `GET /CsLiveHelp/AmCardModalsPartial/{id}` | GET | AccountManager | Fetch comment modal for AM page (validates ownership) |
| `GET /CsLiveHelp/AmCommentThread/{id}` | GET | AccountManager | Fetch fresh comment thread HTML for AM modal (excludes `IsCsInternalOnly` comments, validates ownership) |

All partial endpoints require `X-Requested-With: XMLHttpRequest` header (AJAX only).

---

## File Map

```
Controllers/
  CsLiveHelpController.cs          Main controller for all three pages

Services/
  CsLiveHelpService.cs             EF Core queries and mutation helpers

Hubs/
  CsLiveHelpHub.cs                 SignalR hub — group assignment on connect

Views/CsLiveHelp/
  Requests.cshtml                  AM kanban page
  Board.cshtml                     CS agent kanban page
  RequestsAllBrands.cshtml         CS internal all-brands page
  _CsRequestCard.cshtml            Card partial for AM page
  _CsBoardCard.cshtml              Card partial for CS Board page
  _InternalBoardCard.cshtml        Card partial for All Brands page
  _CsRequestCardModal.cshtml       Comment modal partial injected live for AMs
  _CsBoardCardModals.cshtml        Modal partial injected live for CS Board

wwwroot/js/
  cslivehelp.js                    Shared SignalR client for all three pages

wwwroot/uploads/
  cs-comments/{requestId}/         AM and CS public comment image attachments
  cs-docs/{requestId}/             CS internal document attachments (pdf/doc/xlsx…)
```
