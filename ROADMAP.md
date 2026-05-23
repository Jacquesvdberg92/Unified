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

### 1d-viii – Bug Fixes & Refinement ✅ `[x COMPLETE]`

- [x] CS-internal comments (`RequestsAllBrands`) fully isolated — invisible to AMs (no count, no hint, nothing)
- [x] `Board.cshtml` comment thread shows only AM↔CS comments (`IsCsInternalOnly = false`); locked badge shows CS-internal note count without revealing content
- [x] AM `Requests.cshtml`: Escalated cards land in *In Progress / Escalated* column; no stale state on page refresh
- [x] AM forms (Create, Edit, Delete, AddComment) submit via AJAX — no full-page refresh; DOM driven by SignalR
- [x] `_InternalBoardCard`: shows escalating/assigned CS agent name (`AssignedTo`), not the AM name
- [x] CS Board cards: comment count only reflects AM↔CS thread; *Picked by* agent label added
- [x] **AM image sharing in comments** — AMs can attach a single image per comment (for strange/edge-case evidence); stored as a file reference under `/uploads/cs-comments/{id}/`; rendered inline in the comment thread on both `Requests.cshtml` and `Board.cshtml`; max 5 MB, jpg/png/gif/webp only; CS agents can view but not modify or delete AM attachments
- [ ] Final UI polish pass (spacing, labels, responsive tweaks)

---

## Phase 1e – Account Manager Registration Flow ✅

> Replaces the generic register page for external sign-ups.
> AMs request access via a dedicated flow. Account is locked pending Management approval before login is possible.

### Entry Point – Request Access Page
- [x] `GET /Account/RequestAccess` — page with two options: **Account Manager** and **Other**
- [x] *Other* → redirects to existing `/Account/Register` (CS, Finance, etc.)
- [x] *Account Manager* → redirects to dedicated AM registration page

### AM Registration Page
- [x] `GET /Account/RegisterAccountManager` — form: Full Name, Email, Password, Confirm Password
- [x] `POST /Account/RegisterAccountManager` — creates `AppUser` with `IsExternal = true`, account locked (`LockoutEnabled = true`, `LockoutEnd = DateTimeOffset.MaxValue`) pending approval; role **not** assigned yet
- [x] On successful submit → redirect to `GET /Account/RegistrationPending`
- [x] Rate-limit endpoint: max 3 submissions per IP per hour

### Pending Confirmation Page
- [x] `GET /Account/RegistrationPending` — static message: *"Your request has been received. Please allow 15–30 minutes for approval, then try to log in."*

### Management Approval
- [x] `GET /Admin/PendingAccountManagers` — lists all locked AM applicants (Name, Email, Requested date); Management role only
- [x] `POST /Admin/ApproveAccountManager/{id}` — unlocks account, assigns `AccountManager` role, sets `EmailConfirmed = true`
- [x] `POST /Admin/RejectAccountManager/{id}` — deletes pending user record
- [x] Add "Pending AMs" link to Admin sidebar (Management only)
- [x] Optional: email notification to AM on approval/rejection *(requires email service configured)*

### Login Behaviour for Pending Accounts
- [x] If AM tries to log in while still locked → friendly message: *"Your account is pending approval. Please check back in 15–30 minutes."* (override generic lockout error)

---

## Phase 1f – Activity Logging & Error Tracking ✅

> Centralised middleware-level logging for page visits, user actions, and unhandled errors.
> Covers all authenticated users. `AmAuditLog` stays in place for AM-specific audit trail.

### Activity Logging Middleware
- [x] Audit existing middleware in `Program.cs` — confirmed no duplicate logging pipeline
- [x] Create `ActivityLog` model: `Id, UserId?, UserName?, Action, Path, Method, StatusCode, IpAddress, UserAgent, DurationMs, Timestamp`
- [x] EF Core migration for `ActivityLogs` table + indexes on `(UserId)`, `(Timestamp)`, `(Path)`
- [x] Create `ActivityLoggingMiddleware` — logs every authenticated request (path, method, user, duration, status code)
  - Skip static assets (`/lib/`, `/css/`, `/js/`, `/favicon.ico`) and SignalR hub endpoints
  - Write async (background queue / fire-and-forget) so logging never blocks the request pipeline
- [x] Register middleware in `Program.cs` after authentication, before controllers

### Error Logging
- [x] Create `ErrorLog` model: `Id, UserId?, Path, Method, ExceptionType, Message, StackTrace, Timestamp`
- [x] EF Core migration for `ErrorLogs` table
- [x] Create `GlobalExceptionHandlerMiddleware` — catches unhandled exceptions, persists to `ErrorLogs`, returns user-friendly error page
- [x] Register as the outermost middleware in `Program.cs`

### Admin Log Viewer
- [x] `GET /Admin/ActivityLog` — paginated table (user, path, method, status, duration, timestamp); filterable by user and date range
- [x] `GET /Admin/ErrorLog` — paginated table (exception type, path, user, timestamp); detail drill-down with stack trace
- [x] Both views gated to Admin / Management roles; add to Admin sidebar
- [x] Auto-purge activity logs after configurable retention period (default: 60 days) — extend archive background service or create dedicated one

---

## Phase 2 – AnyDesk ID & Telegram "Log Me In" Bot

### 2a – Data Model
- [x] Add `string? AnydeskId` property to `AppUser`
- [x] Create & apply EF Core migration

### 2b – Admin & Agent UI
- [x] Add AnyDesk ID field to `Views/Admin/Users/Edit.cshtml`
- [x] Add AnyDesk ID field to `Views/Admin/Users/Create.cshtml`
- [x] Save field in `AdminController.EditUser` and `AdminController.CreateUser`
- [ ] Add AnyDesk ID field to agent self-service profile/settings page

### 2c – Telegram Bot Integration
- [x] Add `BotToken` and `ChatId` to `TelegramBotSettings` (admin-configurable via UI)
- [x] Create `Services/TelegramService.cs` with `SendLoginRequestAsync(displayName, anydeskId)`
- [x] Add `POST /Home/RequestLogin` endpoint (reads current user's name + AnyDesk ID, calls service)
- [x] Add rate-limiting (max 1 request per 5 minutes per user)
- [x] Add "Request Login" dashboard widget (visible to all logged-in users)
- [x] Admin `GET/POST /Admin/TelegramSettings` — configure bot token, group chat ID, and enable/disable toggle
- [x] Message format: *"🔐 {DisplayName} needs login — AnyDesk: {AnydeskId}"* (HTML parse mode)

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
- [x] `Sip` model:
  ```
  Id, AuthorId (FK AppUser), Title (max 120), Description (max 2000),
  Category (enum: Improvement | BugReport),
  Status (enum: Open | UnderReview | Accepted | Declined | Implemented),
  CreatedAt, UpdatedAt, OwnerNote?, ScreenshotPath?
  ```
- [x] `SipVote` model:
  ```
  Id, SipId (FK), UserId (FK), IsUpvote (bool), CastAt
  ```
  - Unique constraint on `(SipId, UserId)` — one vote per user per SIP
- [x] DB indexes on `Sip(Status)`, `Sip(AuthorId)`, `Sip(CreatedAt)`
- [x] EF Core migrations `Phase4_Sip` and `Phase4_SipScreenshot`

### 4b – Submission & Listing
- [x] `GET  /Sip` — paginated list of all SIPs; sortable by newest / most votes / status; filterable by category
  - Each row: title, category badge, status badge, net vote score (`upvotes − downvotes`), author, date, owner note (if set)
- [x] `GET  /Sip/Create` — form: Title, Description, Category dropdown
- [x] `POST /Sip/Create` — server-side validation; author from `User.Identity`; initial status `Open`
- [x] `GET  /Sip/Details/{id}` — full description, vote tally, current user's vote state (highlighted), owner note, status badge
- [x] Author can edit or delete their own SIP while status is `Open`

### 4c – Voting
- [x] `POST /Sip/Vote/{id}` — body: `{ isUpvote: bool }`; toggles or changes vote; enforced at DB unique constraint
- [x] Vote submitted via AJAX fetch; score updated inline without page reload
- [x] Authors cannot vote on their own SIP
- [x] Rate-limit: max 20 votes per user per minute (prevent vote-farming)

### 4d – Owner Review Dashboard
- [x] `GET  /Sip/Admin` — Management view: all SIPs ranked by net vote score; status filter tabs
- [x] `POST /Sip/UpdateStatus/{id}` — owner sets status + optional `OwnerNote` (shown publicly on SIP detail)
- [x] Status changes visible to all users immediately
- [x] Owner can delete any SIP (duplicates, spam)
- [x] Page gated to Admin / Management roles only

### 4e – Access & Sidebar
- [x] All internal roles can view, submit, and vote — `AccountManager` role excluded via `[Authorize(Policy = "InternalOnly")]`
- [x] Add *SIP* link to main sidebar (internal users)
- [x] Add *SIP Admin* link to Management sidebar section

---

## Phase 5 – CS-to-CS Direct Messaging & Group Chat

> Real-time direct messaging between CS agents and private group chats.  
> Messages persist in DB with configurable archival (60-day or 300-message cap per conversation).  
> File/image sharing supported (7-day retention). Group creation limits: 3 per agent, 5 per TL, 10 per manager.  
> @mentions trigger notifications (Phase 3). Built on SignalR for real-time push.

### Architecture Decisions
- [!] Real-time: **SignalR** — extend existing hub pattern from Phase 1d for message push
- [!] Message persistence: DB storage with archival — 60-day or 300-message cap per conversation (earliest messages archived first)
- [!] File/image sharing: supported in messages; stored at `/uploads/cs-messages/{conversationId}/`; max 5 MB per file; retained for 7 days then deleted
- [!] Group creation limits: 3 active groups per agent, 5 per TL, 10 per manager; unlimited members per group (team max ~70 people)
- [!] @mentions and notifications: Phase 3 dependency — mention syntax `@{displayName}` triggers push notification to that user
- [!] Direct vs. group: two conversation types — 1:1 (direct) or N-person (group); created by inviting members

### 5a – Data Models & Migration
- [x] Create `CsConversation` model: `Id, Name? (group only), IsGroup (bool), CreatedByUserId (FK), CreatedAt, UpdatedAt, IsArchived`
- [x] Create `CsConversationMember` model: `Id, ConversationId (FK), UserId (FK), JoinedAt, IsActive` — track membership and join time
- [x] Create `CsMessage` model: `Id, ConversationId (FK), AuthorUserId (FK), Body (string, max 5000), CreatedAt, IsEdited, EditedAt?, IsDeleted`
- [ ] Create `CsMessageAttachment` model: `Id, MessageId (FK), FileName (string), FilePath (string), MimeType (string), SizeBytes (long), CreatedAt, ExpiresAt (7-day retention)`
- [x] Create `CsConversationArchive` table (mirror of messages for old/archived conversations)
- [x] DB indexes on `CsConversation(CreatedByUserId)`, `CsConversationMember(UserId, IsActive)`, `CsMessage(ConversationId, CreatedAt)`
- [x] EF Core migration `Phase5_CsMessaging`

### 5b – Backend: Direct & Group Messaging
- [x] `GET /CsMessaging/Conversations` — list of user's active conversations (direct + groups); sorted by last message
- [x] `GET /CsMessaging/Conversation/{id}` — load conversation detail: members, message thread (keyset-paginated, top 50), unread status
- [x] `POST /CsMessaging/StartDirect/{userId}` — initiate 1:1 conversation (or return existing)
- [x] `POST /CsMessaging/CreateGroup` — create new group (name, members list); enforce group creation limits (3/5/10 per role); return `ConversationId`
- [x] `POST /CsMessaging/AddMessage/{id}` — post message to conversation; parse @mentions, extract attachment refs; broadcast via SignalR
- [ ] `POST /CsMessaging/UploadAttachment` — multipart file upload; validate MIME type + size (max 5 MB); store and return attachment ref
- [ ] `POST /CsMessaging/EditMessage/{id}` — edit own message (within 5 min window); mark as edited; broadcast update via SignalR
- [ ] `POST /CsMessaging/DeleteMessage/{id}` — soft-delete own message (or admin/group creator); broadcast deletion via SignalR
- [ ] `POST /CsMessaging/AddMember/{conversationId}/{userId}` — add user to group (group creator or manager); re-broadcast conversation state
- [ ] `POST /CsMessaging/RemoveMember/{conversationId}/{userId}` — remove user from group; broadcast update
- [x] `POST /CsMessaging/MarkRead/{conversationId}` — mark all messages in conversation as read (for unread badge tracking)

### 5c – Real-Time SignalR Hub (CsMessagingHub)
- [x] Create `CsMessagingHub` (SignalR hub)
  - Groups: `cs-messaging` (all CS agents), `conv-{conversationId}` (members of each conversation)
- [ ] Events pushed: `MessageAdded { conversationId, messageId, author, body, mentions[], attachments[], createdAt }`
- [ ] Events pushed: `MessageUpdated { messageId, body, editedAt }`, `MessageDeleted { messageId }`
- [ ] Events pushed: `MemberAdded { conversationId, userId, name }`, `MemberRemoved { conversationId, userId }`
- [ ] Events pushed: `ConversationCreated { conversationId, name, members[] }`
- [x] Register hub in `Program.cs` at `/hubs/cs-messaging`

### 5d – Client JS & Real-Time UI
- [x] Create `wwwroot/js/cs-messaging.js` — connect to hub, handle all push events (message add/update/delete, member add/remove)
- [ ] Message thread: append/update/remove DOM elements in real time; no page reload required
- [x] Unread badge: track unread message count per conversation (via server-side last-read timestamp)
- [ ] Typing indicator (optional): show *"Agent X is typing..."* via hub event
- [ ] @mention autocomplete: as user types `@`, show dropdown of conversation members

### 5e – Views & UI
- [x] `Views/CsMessaging/Index.cshtml` — main messaging hub (sidebar: conversations list, main panel: thread + input form)
- [x] `GET /CsMessaging` — controller action rendering Index view
- [x] Conversation list: active/inactive badge, unread count, last message preview, last-message timestamp
- [ ] Message thread: author name, timestamp, body (with @mention links), attachment thumbnails, edit/delete buttons (own messages only)
- [ ] Input form: textarea (max 5000 chars), file upload button, send button; show attachment list before send
- [ ] Group info modal: name, member list, add/remove member buttons (creator only), archive/leave group buttons
- [x] Sidebar: add "Direct Messages" link under CS Support section (visible to all CS roles)

### 5f – Message Archival & File Cleanup
- [ ] Create `CsMessageArchiveService` (IHostedService) — runs on configurable duty cycle (default: daily)
- [ ] Per conversation: archive messages older than 60 days OR if message count exceeds 300 (whichever comes first); move to `CsConversationArchive`
- [ ] Add `CsMessaging:Archive` config section to `appsettings.json` (defaults: `RunEveryHours = 24`, `MessageAgeThresholdDays = 60`, `MessageCountThreshold = 300`)
- [ ] File attachment cleanup: delete files older than 7 days (separate cleanup pass via `ExpiresAt` column)
- [ ] Register archive service in `Program.cs`

### 5g – Controller & Authorization
- [ ] `[Authorize(Roles = $"{Roles.CSAgent},{Roles.TeamLeader},{Roles.BrandManager},{Roles.SwissArmyKnife}")]` on `CsMessagingController` — block Account Managers
- [x] Server-side validation: user membership in conversation before allowing message post/edit/delete
- [ ] Rate-limiting: max 30 messages per user per minute (prevent chat spam)
- [ ] Audit log: optional — track group creation, member adds/removes for compliance

### 5h – @Mention & Notification Integration (Phase 3 Dependency)
- [ ] Parse message body for `@{displayName}` syntax (case-insensitive matching to user list)
- [ ] For each @mention, trigger `NotificationService` (Phase 3) — create notification for mentioned user
- [ ] Notification type: "Mentioned in Direct Message" or "Mentioned in Group: {groupName}"
- [ ] Render @mentions in message thread as links (or highlighted spans) pointing to that user's profile

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

*Last updated: 2025-06 — Phase 1d bug fixing complete | Phase 1e AM registration flow complete | Phase 1f activity logging complete | Phase 4 SIP complete | Phase 5 CS-to-CS messaging in progress (emoji, reactions, GIF support live) | Owner: @Jacquesvdberg92*
