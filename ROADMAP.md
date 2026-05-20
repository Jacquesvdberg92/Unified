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

## Phase 1d – CS Live Help Overhaul (Kanban + AM Requests) `[ NOT STARTED ]`

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

### 1d-i – Data Models & Migration
- [ ] Create `CsRequest` model:
  ```
  Id, AccountManagerId (FK AppUser), BrandId (FK), RequestTypeId (FK),
  CustomDescription (string?, English-only, max 500 chars),
  Status (enum: Open | InProgress | OnGoing | Completed | Escalated),
  CreatedAt, UpdatedAt, ArchivedAt?
  ```
- [ ] Create `CsRequestType` lookup table:
  ```
  Id, Name (e.g. "Simulate POI", "Reset Password", "Other"), IsOther (bool)
  ```
- [ ] Create `CsRequestComment` model (thread per card):
  ```
  Id, RequestId (FK), AuthorId (FK AppUser), Body (string), CreatedAt, IsSystemMessage (bool)
  ```
- [ ] Create `CsRequestArchive` table (identical schema to `CsRequest` – for completed/old records)
- [ ] Create `AmAuditLog` model: `Id, UserId, Action, EntityId?, Timestamp, IpAddress`
- [ ] Write and apply EF Core migration for all above

### 1d-ii – Backend: AM Request Actions
- [ ] `GET  /CsLiveHelp/Requests` — AM view: their own cards (Kanban) **plus a read-only "Others" column** showing other AMs' open requests for the same brand — prevents duplicate submissions
- [ ] `POST /CsLiveHelp/CreateRequest` — AM submits a new request
  - Inputs: `brandId` (dropdown), `requestTypeId` (dropdown incl. "Other"), `customDescription` (if Other, English-only regex/language check)
  - Validation: antiforgery, model validation, rate limit, AM audit log entry
- [ ] `POST /CsLiveHelp/EditRequest/{id}` — AM edits their own Open request only
- [ ] `POST /CsLiveHelp/DeleteRequest/{id}` — AM deletes their own Open request only
- [ ] `POST /CsLiveHelp/AddComment/{id}` — AM adds a comment to their own request thread

### 1d-iii – Backend: CS Agent Actions
- [ ] `GET  /CsLiveHelp/Board` — CS agents see ALL open requests across all AMs (Kanban board)
- [ ] `POST /CsLiveHelp/UpdateStatus/{id}` — CS moves card: Open → InProgress → OnGoing → Completed
- [ ] `POST /CsLiveHelp/AddComment/{id}` — CS adds comment to thread (shared endpoint with AM, role-gated body)
- [ ] **Smart action buttons per request type** (shown on card to CS only):
  - `Simulate POI` request → button: **"Log Simulation"** → calls existing POI simulation log flow
  - `Reset Password` request → button: **"Send Reset"** → posts system comment: *"Password reset to Aa123456"*
  - `Escalated` card → button: **"Mark Passed"** → posts system comment: *"Passed to relevant agents"*
- [ ] `POST /CsLiveHelp/Escalate/{id}` — CS escalates card (Status = Escalated); card moves to Escalated column

### 1d-iv – Backend: CS Team Leader / Manager View + CS Team Chat
- [ ] `GET  /CsLiveHelp/RequestsAllBrands` — **CS Team Chat / All Requests board**
  - Visible to: all CS roles (agents, TL, Manager) — this is a shared internal space, not AM-facing
  - Shows **all open requests across all brands** — CS agents can post requests here directly (not coming from an AM), or "pass" an AM request here for team-wide visibility
  - Think of it as a **CS-internal chat/notice board**: CS raises issues, asks questions, passes escalated items — similar in spirit to CS Live Help but team-internal rather than AM-facing
  - TL/Manager additionally see a filtered **Escalated** view by default
  - *(Full feature details TBD — adding more specifics later)*
- [ ] `POST /CsLiveHelp/ResolveEscalation/{id}` — Mark escalated request as resolved/completed
- [ ] Add "Requests – All Brands" link to Management sidebar section (TL/Manager only); add "Team Chat" link for CS agents

### 1d-v – Real-Time SignalR Hub
- [ ] Create `CsLiveHelpHub` (SignalR hub)
  - Groups: `cs-board` (all CS agents), `am-{userId}` (each AM sees only their own cards)
  - Events pushed: `CardAdded`, `CardUpdated`, `CardStatusChanged`, `CardDeleted`, `CommentAdded`
- [ ] Register hub in `Program.cs` at `/hubs/cslivehelp`
- [ ] Client JS (`wwwroot/js/cslivehelp.js`): connect to hub, handle push events to update board DOM without full reload
- [ ] CS Board JS: drag-and-drop columns map to status transitions (call `UpdateStatus` on drop)

### 1d-vi – Performance & Duty Cycle
- [ ] Add DB indexes: `CsRequest(Status)`, `CsRequest(CreatedAt)`, `CsRequest(BrandId)`, `CsRequest(AccountManagerId)`
- [ ] Implement cursor/keyset pagination for board columns (no OFFSET – use `Id` cursor)
- [ ] Create `CsRequestArchiveService` (background `IHostedService`):
  - Interval and age threshold read from `appsettings.json` section `CsLiveHelp:Archive` — defaults: `RunEveryHours = 6`, `CompleteAgeThresholdDays = 3`
  - Moves `Completed` requests older than threshold to `CsRequestArchive`
  - Logs archive run (interval, count moved) to application log
- [ ] Add `CsLiveHelp:Archive` config section to `appsettings.json`
- [ ] Register archive service in `Program.cs`
- [ ] Board columns load top 50 cards; "Load more" button fetches next page via AJAX

### 1d-vii – Views
- [ ] `Views/CsLiveHelp/Requests.cshtml` — AM Kanban view (own cards only: Open, InProgress, Completed)
- [ ] `Views/CsLiveHelp/Board.cshtml` — CS Kanban view (all cards: Open, InProgress, OnGoing, Escalated, Completed)
- [ ] `Views/CsLiveHelp/RequestsAllBrands.cshtml` — TL/Manager escalation view
- [ ] Shared partial `_CsRequestCard.cshtml` — single card component (used in all 3 views)
- [ ] Shared partial `_CsRequestThread.cshtml` — comment thread modal/drawer per card
- [ ] Update sidebar: add "CS Requests" link for AM role; add "All Brands" under Management section

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

## Ongoing / Cross-Cutting

- [ ] Review all controller `[Authorize]` attributes after new roles are added
- [ ] Ensure AM login redirect goes directly to `/CsLiveHelp/Requests`
- [x] Add Roadmap view to the app sidebar *(done – `Views/Home/Roadmap.cshtml` + `HomeController.Roadmap()` action)*
- [x] Implement `EmailTemplatesController` *(was empty — full CRUD + brand/document management implemented)*
- [ ] Write unit tests for `CsRequestArchiveService`, `TelegramService`, `NotificationService`
- [ ] Update `DEMO_SEED_PLAN.md` with seed data for `CsRequestType` lookup table
- [ ] Load/performance test CS Live Help board at simulated high volume before go-live

---

*Last updated: 2025-05-22 | Owner: @Jacquesvdberg92*
