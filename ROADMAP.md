# Unified – Project Roadmap

> Track the status of all planned work. Update `[ ]` to `[x]` as tasks are completed.

---

## Legend
| Symbol | Meaning |
|--------|---------|
| `[ ]`  | Not started |
| `[~]`  | In progress |
| `[x]`  | Completed |
| `[!]`  | Blocked / needs clarification |

---

## Phase 0 – Structural Fix (Misplaced Files) ✅

> Several files were accidentally nested inside `Unified\Unified\` instead of the project root. These must be resolved before any new feature work.

- [x] Audit all files under `Unified\Unified\` vs the project root
- [x] Move unique files to correct locations (Controllers, Models, Services)
  - `Unified\Unified\Controllers\EmailTemplatesController.cs` *(was misplaced and empty — rebuilt)*
  - `Unified\Unified\Controllers\ProcessTemplatesController.cs`
  - `Unified\Unified\Models\EmailTemplates\EmailTemplate.cs`
  - `Unified\Unified\Models\ProcessTemplates\ProcessTemplate.cs`
  - `Unified\Unified\Models\ProcessTemplates\ProcessTemplateBrand.cs`
  - `Unified\Unified\Models\ProcessTemplates\TemplateCategory.cs`
  - `Unified\Unified\Services\EmailTemplateService.cs`
  - `Unified\Unified\Services\ProcessTemplateService.cs`
  - `Unified\Unified\Program.cs` *(was empty — deleted)*
  - `Unified\Unified\Views\PoiSimulation\_LogPartial.cshtml` *(moved to `Views\PoiSimulation\`)*
  - `Unified\Unified\wwwroot\js\dashboard-widgets.js` *(was duplicate — deleted)*
- [x] Remove misplaced duplicates after verifying content is merged/preserved
- [x] Created missing `Models\ErrorViewModel.cs` (was absent — caused build error)
- [x] Update `.csproj` includes if any paths were hardcoded *(SDK-style project — no changes needed)*
- [x] Build with zero errors after cleanup

---

## Phase 1 – New Roles ✅ *(complete through 1c; Phase 1d not yet started)*

### 1a – Role Constants & Seeding ✅
- [x] Add `AccountManager` (`AM`) constant to `Models/Identity/Roles.cs`
- [x] Add `Finance` constant to `Models/Identity/Roles.cs`
- [x] Seed both new roles in `Program.cs` / `SeedData.cs`

### 1b – Account Manager (AM) Role ✅
> External users. Sign up via the normal registration flow and are assigned the `AccountManager` role.  
> Access is strictly limited to **CS Live Help** only — all other pages must return 403/redirect.

- [ ] Add `AccountManager` to `[Authorize]` on `CsLiveHelpController` (AM-facing request actions only) *(Phase 1d)*
- [x] Block `AccountManager` from all other controllers (`InternalOnly` policy registered in `Program.cs`)
- [x] After login, redirect `AccountManager` users directly to `/CsLiveHelp/Requests` (their only landing page)
- [x] Add `AccountManager` to Admin `PopulateRolesAndTeams` dropdown
- [x] Add `IsExternal` flag to `AppUser` + EF migration `AddIsExternalToAppUser`
- [ ] Apply strict server-side input validation on all AM-submitted data (treat as untrusted/hostile) *(Phase 1d)*
- [ ] Rate-limit all AM POST actions (e.g. max 30 requests/minute per user via middleware or filter) *(Phase 1d)*
- [ ] Log every AM action to an audit trail table (`AmAuditLog`: UserId, Action, Timestamp, IP) *(Phase 1d)*

### 1c – Finance Role ✅
> Internal. Access to Schedule and Check-in/Check-out data (read-only, Phase 1).

- [x] Add `Finance` to `[Authorize]` on `ScheduleController`
- [x] Add `Finance` to `[Authorize]` on `AttendanceReportController`
- [x] Add `Finance` to Admin `PopulateRolesAndTeams` dropdown
- [ ] Define additional Finance data requirements for Phase 2 *(needs detail from owner — deferred)*

---

## Phase 1d – CS Live Help Overhaul (Kanban + AM Requests) `[~ BUG FIXING & REFINEMENT]`

> **Context:** This is a **brand new feature**, entirely separate from the existing CS Live Help duty schedule.  
> It introduces a Kanban-style live board where Account Managers (external) submit support requests  
> and CS agents work, respond, and escalate them.  
> **Which CS agents see the board** is determined by Work Distribution — agents allocated to a CS Live Help slot  
> for a given time window are considered "on duty" and are the ones actively working the board.  
> The duty schedule and Work Distribution systems are not modified by this feature.

### Architecture Decisions
- **Real-time transport:** SignalR (lightweight hub, push-only to clients, no polling)
- **Performance target:** Hundreds–thousands of requests/day. Board must stay responsive.
  - Server: paginate/virtualise board columns (load top N cards, fetch more on scroll)
  - Client: SignalR push diffs only (card added/updated/deleted), never reload full board
  - DB: index on `Status`, `CreatedAt`, `BrandId`; archive old completed cards on a duty cycle
  - Duty cycle: background service runs every 6 hours to archive cards; interval and age threshold are configurable via `appsettings.json` (default: run every 6 h, archive `Completed` cards older than **3 days**)
  - **Duplicate prevention:** AMs can see each other's open requests (read-only, no comments). If a request for the same brand + type is already open, a warning is shown before submit.
- **Security:** AMs are untrusted external actors. All input sanitised, HTML-encoded, English-only enforced on "Other" field

### 1d-i – Data Models & Migration ✅
- [x] Create `CsRequest` model:
  ```
  Id, AccountManagerId (FK AppUser), BrandId (FK), RequestTypeId (FK),
  CustomDescription (string?, English-only, max 500 chars),
  Status (enum: Open | InProgress | OnGoing | Completed | Escalated),
  CreatedAt, UpdatedAt, ArchivedAt?
  ```
- [x] Create `CsRequestType` lookup table:
  ```
  Id, Name (e.g. "Simulate POI", "Reset Password", "Other"), IsOther (bool)
  ```
- [x] Create `CsRequestComment` model (thread per card):
  ```
  Id, RequestId (FK), AuthorId (FK AppUser), Body (string), CreatedAt, IsSystemMessage (bool)
  ```
- [x] Create `CsRequestArchive` table (identical schema to `CsRequest` – for completed/old records)
- [x] Create `AmAuditLog` model: `Id, UserId, Action, EntityId?, Timestamp, IpAddress`
- [x] Write and apply EF Core migration for all above (`Phase1d_CsLiveHelpModels`)

### 1d-ii – Backend: AM Request Actions ✅
- [x] `GET  /CsLiveHelp/Requests` — AM view: their own cards (Kanban) **plus a read-only "Others" column** showing other AMs' open requests for the same brand — prevents duplicate submissions
- [x] `POST /CsLiveHelp/CreateRequest` — AM submits a new request
  - Inputs: `brandId` (dropdown), `requestTypeId` (dropdown incl. "Other"), `customDescription` (if Other, English-only regex/language check)
  - Validation: antiforgery, model validation, rate limit, AM audit log entry
- [x] `POST /CsLiveHelp/EditRequest/{id}` — AM edits their own Open request only
- [x] `POST /CsLiveHelp/DeleteRequest/{id}` — AM deletes their own Open request only
- [x] `POST /CsLiveHelp/AddComment/{id}` — AM adds a comment to their own request thread

### 1d-iii – Backend: CS Agent Actions ✅
- [x] `GET  /CsLiveHelp/Board` — CS agents see ALL open requests across all AMs (Kanban board)
- [x] `POST /CsLiveHelp/UpdateStatus/{id}` — CS moves card: Open → InProgress → OnGoing → Completed
- [x] `POST /CsLiveHelp/CsAddComment/{id}` — CS adds comment to thread (role-gated, not ownership-gated)
- [x] **Smart action buttons per request type** (shown on card to CS only):
  - `Simulate POI` request → button: **"Log Simulation"** → opens POI log modal pre-populated with card's brand
  - `Reset Password` request → button: **"Send Reset"** → posts system comment: *"Password reset to Aa123456"* + marks Completed
  - `Escalated` card → button: **"Mark Passed"** → posts system comment: *"Passed to relevant agents"* + marks Completed
- [x] `POST /CsLiveHelp/Escalate/{id}` — CS escalates card (Status = Escalated); card moves to Escalated column

### 1d-iv – Backend: CS Team Leader / Manager View + Requests All Brands (Internal Board) ✅
> **Context:** This is a CS-internal escalation and notice board — not AM-facing.
> Escalated cards from CS Live Help become visible here automatically.
> All CS roles can comment and update cards on this board.
> Performance and pagination model mirrors the CS Live Help board (top 50, keyset cursor, same architecture).

- [x] `GET  /CsLiveHelp/RequestsAllBrands` — **Internal CS board**
  - **Default audience:** CS Team Leaders and Managers see all brands, all escalated items
  - **Escalation visibility:** when any card is escalated (from CS Live Help or internal), it becomes visible to **all CS agents** on this board — not just TL/Manager
  - **Notifications (Phase 1d-ix — deferred):** on escalation, notify agents allocated to the escalated card's brand *and* the language assigned during the escalation process; notification system not yet built — placeholder only
  - CS agents can also **post new internal requests directly** on this board (not originating from an AM)
  - All CS roles (agent, TL, Manager) can post comments and update card status on this board
  - TL/Manager default filter view: Escalated; agents default: all open
- [x] `POST /CsLiveHelp/ResolveEscalation/{id}` — TL/Manager marks escalated card resolved/completed
- [ ] **Image attachments on CS Live Help board (AM-facing)** — AMs can attach images to requests and comments; stored as blobs or file references; max size TBD; rendered inline on the card thread
- [ ] **File attachments on Requests All Brands (internal board)** — CS agents/TL/Manager can attach files (images, docs, etc.) to internal board posts and comments; same storage pattern as AM image attachments
- [x] Add "Requests – All Brands" link to Management sidebar section (TL/Manager only); add "Team Board" link for CS agents

### 1d-v – UX / Polish (discovered during testing)
- [x] **Comment visibility** — CS Board cards have no way to read existing comments; add a "View Thread" button/modal that shows the full comment thread (author, timestamp, body, system-message flag) before posting a reply
- [x] **Drag-and-drop column transitions** — replace the "Move" button modal with SortableJS so agents can drag cards between status columns; drop fires `POST /CsLiveHelp/UpdateStatusJson/{id}` via fetch
- [x] **Auto-assign on move** — when a CS agent moves a card (status transition), record `AssignedToId` on the `CsRequest` and show the assigned agent's name on the card; `AssignedToId` FK + EF migration `Phase1dv_AssignedTo` applied
- [x] **Real-time board updates** — SignalR hub (1d-vi) pushes `CardAdded`, `CardUpdated`, `CardDeleted`, `CardStatusChanged`, `CommentAdded`; board DOM updated without page refresh
- [x] **POI Simulation smart action — modal instead of redirect** — "Log Simulation" button on a CS board card opens the POI log modal pre-populated with the card's brand via `/PoiSimulation/LogPartialWithBrand?brandId={id}`

### 1d-vi – Real-Time SignalR Hub
- [x] Create `CsLiveHelpHub` (SignalR hub)
  - Groups: `cs-board` (all CS agents), `am-{userId}` (each AM sees only their own cards)
  - Events pushed: `CardAdded`, `CardUpdated`, `CardStatusChanged`, `CardDeleted`, `CommentAdded`
- [x] Register hub in `Program.cs` at `/hubs/cslivehelp`
- [x] Client JS (`wwwroot/js/cslivehelp.js`): connect to hub, handle push events to update board DOM without full reload
- [x] CS Board JS: drag-and-drop columns already wired; SignalR events (`CardStatusChanged`) move cards across columns in real time

### 1d-vi – Performance & Duty Cycle
- [x] Add DB indexes: `CsRequest(Status)`, `CsRequest(CreatedAt)`, `CsRequest(BrandId)`, `CsRequest(AccountManagerId)` — already present in `AppDbContext.cs`
- [x] Implement cursor/keyset pagination for board columns (no OFFSET – use `Id` cursor) — `GetBoardRequestsAsync(afterId)` already uses `Where(r => r.Id > afterId).Take(50)`
- [x] Create `CsRequestArchiveService` (background `IHostedService`)
  - Interval and age threshold read from `appsettings.json` section `CsLiveHelp:Archive` — defaults: `RunEveryHours = 6`, `CompleteAgeThresholdDays = 3`
  - Moves `Completed` requests older than threshold to `CsRequestArchive`
  - Logs archive run (interval, count moved) to application log
- [x] Add `CsLiveHelp:Archive` config section to `appsettings.json`
- [x] Register archive service in `Program.cs`
- [x] Board columns load top 50 cards; "Load more" button fetches next page via `GET /CsLiveHelp/BoardPage?status=X&afterId=Y`

### 1d-vii – Views
- [x] `Views/CsLiveHelp/Requests.cshtml` — AM Kanban view (own cards only: Open, InProgress, Completed)
- [x] `Views/CsLiveHelp/Board.cshtml` — CS Kanban view (all cards: Open, InProgress, OnGoing, Escalated, Completed)
- [x] `Views/CsLiveHelp/RequestsAllBrands.cshtml` — TL/Manager & CS escalation board
- [x] Shared partials: `_CsRequestCard.cshtml`, `_CsBoardCard.cshtml`, `_InternalBoardCard.cshtml`
- [x] Sidebar: CS Requests for AM; CS Board for CS roles; All Brands for TL/Manager

---

### 1d-viii – Bug Fixes & Refinement `[~ ACTIVE]`

- [x] CS-internal comments (`RequestsAllBrands`) fully isolated — invisible to AMs (no count, no hint, nothing)
- [x] `Board.cshtml` comment thread shows only AM↔CS comments (`IsCsInternalOnly = false`); locked badge shows CS-internal note count without revealing content
- [x] AM `Requests.cshtml`: Escalated cards land in *In Progress / Escalated* column; no stale state on page refresh
- [x] AM forms (Create, Edit, Delete, AddComment) submit via AJAX — no full-page refresh; DOM driven by SignalR
- [x] `_InternalBoardCard`: shows escalating/assigned CS agent name (`AssignedTo`), not the AM name
- [x] CS Board cards: comment count only reflects AM↔CS thread; *Picked by* agent label added
- [~] **AM image sharing in comments** — AMs can attach a single image per comment (for strange/edge-case evidence); stored as a file reference; rendered inline in the comment thread on both `Requests.cshtml` and `Board.cshtml`; max 5 MB, image types only (jpg/png/gif/webp); CS agents can view but not modify or delete AM attachments
- [ ] Final UI polish pass (spacing, labels, responsive tweaks)

---

## Phase 1e – Account Manager Registration Flow

> Replaces the generic register page for external sign-ups.
> AMs request access via a dedicated flow. Account is locked pending Management approval before login is possible.

### Entry Point – Request Access Page
- [ ] `GET /Account/RequestAccess` — page with two options: **Account Manager** and **Other**
- [ ] *Other* → redirects to existing `/Account/Register` (CS, Finance, etc.)
- [ ] *Account Manager* → redirects to dedicated AM registration page

### AM Registration Page
- [ ] `GET /Account/RegisterAccountManager` — form: Full Name, Email, Password, Confirm Password
- [ ] `POST /Account/RegisterAccountManager` — creates `AppUser` with `IsExternal = true`, account locked (`LockoutEnabled = true`, `LockoutEnd = DateTimeOffset.MaxValue`) pending approval; role **not** assigned yet
- [ ] On successful submit → redirect to `GET /Account/RegistrationPending`
- [ ] Rate-limit endpoint: max 3 submissions per IP per hour

### Pending Confirmation Page
- [ ] `GET /Account/RegistrationPending` — static message: *"Your request has been received. Please allow 15–30 minutes for approval, then try to log in."*

### Management Approval
- [ ] `GET /Admin/PendingAccountManagers` — lists all locked AM applicants (Name, Email, Requested date); Management role only
- [ ] `POST /Admin/ApproveAccountManager/{id}` — unlocks account, assigns `AccountManager` role, sets `EmailConfirmed = true`
- [ ] `POST /Admin/RejectAccountManager/{id}` — deletes pending user record
- [ ] Add "Pending AMs" link to Admin sidebar (Management only)
- [ ] Optional: email notification to AM on approval/rejection *(requires email service configured)*

### Login Behaviour for Pending Accounts
- [ ] If AM tries to log in while still locked → friendly message: *"Your account is pending approval. Please check back in 15–30 minutes."* (override generic lockout error)

---

## Phase 1f – Activity Logging & Error Tracking

> Centralised middleware-level logging for page visits, user actions, and unhandled errors.
> Covers all authenticated users. `AmAuditLog` stays in place for AM-specific audit trail.

### Activity Logging Middleware
- [ ] Audit existing middleware in `Program.cs` — confirm no duplicate logging pipeline
- [ ] Create `ActivityLog` model: `Id, UserId?, UserName?, Action, Path, Method, StatusCode, IpAddress, UserAgent, DurationMs, Timestamp`
- [ ] EF Core migration for `ActivityLogs` table + indexes on `(UserId)`, `(Timestamp)`, `(Path)`
- [ ] Create `ActivityLoggingMiddleware` — logs every authenticated request (path, method, user, duration, status code)
  - Skip static assets (`/lib/`, `/css/`, `/js/`, `/favicon.ico`) and SignalR hub endpoints
  - Write async (background queue / fire-and-forget) so logging never blocks the request pipeline
- [ ] Register middleware in `Program.cs` after authentication, before controllers

### Error Logging
- [ ] Create `ErrorLog` model: `Id, UserId?, Path, Method, ExceptionType, Message, StackTrace, Timestamp`
- [ ] EF Core migration for `ErrorLogs` table
- [ ] Create `GlobalExceptionHandlerMiddleware` — catches unhandled exceptions, persists to `ErrorLogs`, returns user-friendly error page
- [ ] Register as the outermost middleware in `Program.cs`

### Admin Log Viewer
- [ ] `GET /Admin/ActivityLog` — paginated table (user, path, method, status, duration, timestamp); filterable by user and date range
- [ ] `GET /Admin/ErrorLog` — paginated table (exception type, path, user, timestamp); detail drill-down with stack trace
- [ ] Both views gated to Admin / Management roles; add to Admin sidebar
- [ ] Auto-purge activity logs after configurable retention period (default: 90 days) — extend archive background service or create dedicated one

---

## Phase 2 – AnyDesk ID & Telegram "Log Me In" Bot

### 2a – Data Model
- [ ] Add `string? AnydeskId` property to `AppUser`
- [ ] Create & apply EF Core migration

### 2b – Admin & Agent UI
- [ ] Add AnyDesk ID field to `Views/Admin/Users/Edit.cshtml`
- [ ] Add AnyDesk ID field to `Views/Admin/Users/Create.cshtml`
- [ ] Save field in `AdminController.EditUser` and `AdminController.CreateUser`
- [ ] Add AnyDesk ID field to agent self-service profile/settings page

### 2c – Telegram Bot Integration
- [ ] Add `Telegram.Bot` NuGet package
- [ ] Add `BotToken` and `ItChannelChatId` to `appsettings.json` (use User Secrets for dev)
- [ ] Create `Services/TelegramService.cs` with `PostLoginRequestAsync(displayName, anydeskId)`
- [ ] Add `POST /Home/RequestLogin` endpoint (reads current user's name + AnyDesk ID, calls service)
- [ ] Add rate-limiting (max 1 request per 5 minutes per user)
- [ ] Add "Request Google Login" button to dashboard/home page (visible to all logged-in users)
- [ ] Test Telegram message format: *"🔐 {DisplayName} needs Google login — AnyDesk: {AnydeskId}"*

---

## Phase 3 – Notification System

> Details to be provided by owner. Outline only – refine before implementation.

### 3a – Infrastructure
- [ ] Confirm real-time transport: **SignalR** (reuse hub pattern from Phase 1d) *(confirm with owner)*
- [ ] Create `Notification` model: `Id, UserId, Title, Body, Link, IsRead, CreatedAt, Type`
- [ ] Create EF Core migration for `Notifications` table
- [ ] Create `NotificationService` (create, mark-read, get-unread)
- [ ] Create `NotificationHub` (or extend `CsLiveHelpHub`)
- [ ] Register hub in `Program.cs`

### 3b – Client UI
- [ ] Bell icon in navbar with live unread badge (SignalR-driven)
- [ ] Notification dropdown panel (list recent, mark-as-read)
- [ ] Audio alert on receive (`new Audio(...).play()`)
- [ ] Define notification types and sound levels *(needs owner decision)*

### 3c – Preferences & Admin Broadcast
- [ ] User notification preferences page (per-type + audio toggle)
- [ ] Admin "Send System Notification" broadcast to all connected clients

---

## Phase 4 – System Improvement Proposals (SIP)

> Internal users (all roles **except** Account Managers) can submit improvement proposals or bug reports.
> Colleagues cast upvotes / downvotes. The owner reviews community feedback and decides whether to schedule each item.
> Account Managers are explicitly excluded — SIP is an internal tool only.

### 4a – Data Models & Migration
- [ ] `Sip` model:
  ```
  Id, AuthorId (FK AppUser), Title (max 120), Description (max 2000),
  Category (enum: Improvement | BugReport),
  Status (enum: Open | UnderReview | Accepted | Declined | Implemented),
  CreatedAt, UpdatedAt, OwnerNote?
  ```
- [ ] `SipVote` model:
  ```
  Id, SipId (FK), UserId (FK), IsUpvote (bool), CastAt
  ```
  - Unique constraint on `(SipId, UserId)` — one vote per user per SIP
- [ ] DB indexes on `Sip(Status)`, `Sip(AuthorId)`, `Sip(CreatedAt)`
- [ ] EF Core migration `Phase4_Sip`

### 4b – Submission & Listing
- [ ] `GET  /Sip` — paginated list of all SIPs; sortable by newest / most votes / status; filterable by category
  - Each row: title, category badge, status badge, net vote score (`upvotes − downvotes`), author, date, owner note (if set)
- [ ] `GET  /Sip/Create` — form: Title, Description, Category dropdown
- [ ] `POST /Sip/Create` — server-side validation; author from `User.Identity`; initial status `Open`
- [ ] `GET  /Sip/Details/{id}` — full description, vote tally, current user's vote state (highlighted), owner note, status badge
- [ ] Author can edit or delete their own SIP while status is `Open`

### 4c – Voting
- [ ] `POST /Sip/Vote/{id}` — body: `{ isUpvote: bool }`; toggles or changes vote; enforced at DB unique constraint
- [ ] Vote submitted via AJAX fetch; score updated inline without page reload
- [ ] Authors cannot vote on their own SIP
- [ ] Rate-limit: max 20 votes per user per minute (prevent vote-farming)

### 4d – Owner Review Dashboard
- [ ] `GET  /Sip/Admin` — Management view: all SIPs ranked by net vote score; status filter tabs
- [ ] `POST /Sip/UpdateStatus/{id}` — owner sets status + optional `OwnerNote` (shown publicly on SIP detail)
- [ ] Status changes visible to all users immediately
- [ ] Owner can delete any SIP (duplicates, spam)
- [ ] Page gated to Admin / Management roles only

### 4e – Access & Sidebar
- [ ] All internal roles can view, submit, and vote — `AccountManager` role excluded via `[Authorize(Policy = "InternalOnly")]`
- [ ] Add *SIP* link to main sidebar (internal users)
- [ ] Add *SIP Admin* link to Management sidebar section

---

## Ongoing / Cross-Cutting

- [ ] Review all controller `[Authorize]` attributes after new roles are added
- [ ] Ensure AM login redirect goes directly to `/CsLiveHelp/Requests`
- [x] Add Roadmap view to the app sidebar *(done – `Views/Home/Roadmap.cshtml` + `HomeController.Roadmap()` action)*
- [x] Implement `EmailTemplatesController` *(was empty — full CRUD + brand/document management implemented)*
- [ ] Write unit tests for `CsRequestArchiveService`, `TelegramService`, `NotificationService`
- [ ] Update `DEMO_SEED_PLAN.md` with seed data for `CsRequestType` lookup table
- [ ] Load/performance test CS Live Help board at simulated high volume before go-live

---

*Last updated: 2025-06 — Phase 1d bug fixing & refinement active | AM image sharing planned | Phase 1e AM registration flow added | Phase 1f activity & error logging added | Phase 4 SIP added | Owner: @Jacquesvdberg92*
