# Unified — Agent Build Plan

> **Purpose:** Step-by-step build plan for AI agents and developers.  
> Tick each checkbox `[x]` when the step is complete.  
> Phases are ordered; do not start a phase until the previous one is marked **DONE**.

---

## Legend
| Symbol | Meaning |
|--------|---------|
| 🧹 | Cleanup / housekeeping |
| 🏗️ | Scaffolding / structure |
| ✉️ | Email Templates module |
| 📢 | Updates / Noticeboard module |
| 🗓️ | Schedule Manager module |
| 📊 | Reports module |
| 🔐 | Auth & Roles |
| 👤 | Agent Management & Performance Reviews |
| 🔑 | Login Vault |
| 📋 | Process Templates |
| ✅ | Test / verify |

---

## Phase 0 — Solution Cleanup 🧹
> Goal: strip everything unused from the starter kit and establish the clean base.

- [x] **0.1** Delete demo/unused image folders  
  - `wwwroot/assets/images/` — remove all sub-folders except `brands/` (create empty placeholder)  
  - `wwwroot/assets/video/` — delete `1.mp4`  
- [x] **0.2** Remove unused third-party libs from `wwwroot/assets/libs/`  
  Keep only: `bootstrap`, `@popperjs`, `simplebar`, `node-waves`, `sweetalert2`, `toastify-js`, `datatables.net-bs5`, `apexcharts`, `flatpickr`, `quill`, `sortablejs`, `fullcalendar`  
  Remove everything else (animejs, dragula, dropzone, dual-listbox, echarts, filepond-*, glightbox, gmaps, gridjs, isotope-layout, jsvectormap, leaflet, particles.js, plyr, prismjs, rater-js, shepherd.js, slick-slider, swiper, vanilla-wizard, etc.)  
- [x] **0.3** Remove `Legal Agreement & Copyright Notice.txt` (template vendor file, not needed in repo)  
- [x] **0.4** Remove `starterkit.csproj.user` from source control (add to `.gitignore`)  
- [x] **0.5** Clean up `Views/Home/index.cshtml` — replace demo content with a simple dashboard stub  
- [x] **0.6** Clean up `Views/Shared/_Layout.cshtml` — strip demo nav links, leave only sidebar shell  
- [x] **0.7** Remove unused SCSS pages: `wwwroot/assets/scss/pages/` — delete any page-specific demo files  
- [x] **0.8** Add `.gitignore` entries: `obj/`, `bin/`, `*.user`, `node_modules/`  
- [x] **0.9** Verify build passes: `dotnet build`  

**Phase 0 Status:** `[x] IN PROGRESS` → `[x] DONE`

---

## Phase 1 — Foundation & Auth 🔐 🏗️
> Goal: role-based authentication, multi-brand/team structure, and full agent profile so every module can scope data correctly.

### Roles
Four roles are supported. Permissions build upward — each role inherits the view access of the one below it.

| Role | Key Capabilities |
|------|------------------|
| **BrandManager** (`Admin`) | Full system access: brands, users, roles, all reports, all settings |
| **TeamLeader** | Post updates, manage schedules, submit reports, create/edit email templates, write performance reviews |
| **CSAgent** | View own schedule, view updates feed, view own performance reviews, basic dashboard |
| **SwissArmyKnife** | Same view access as CSAgent but flagged as cross-team/cross-brand; appears in all team rosters and schedule views; eligible for any brand task |

### Data Layer
- [x] **1.1** Add NuGet packages:  
  - `Microsoft.EntityFrameworkCore.Sqlite` (dev) + `Microsoft.EntityFrameworkCore.SqlServer` (prod)  
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`  
  - `Microsoft.EntityFrameworkCore.Tools`  
- [x] **1.2** Create `Data/AppDbContext.cs` inheriting `IdentityDbContext<AppUser>`  
- [x] **1.3** Create `Models/Identity/AppUser.cs` extending `IdentityUser` with:  
  - `string DisplayName`  
  - `string? AvatarUrl`  
  - `string? Language` — primary language (e.g. `"EN"`, `"JP"`, `"PT"`)  
  - `bool IsSwissArmyKnife` — convenience flag mirroring the SAK role  
  - `bool HasWeekendShift` — default shift pattern flag (used by schedule wheel)  
  - `ICollection<AgentTeam> Teams` — many-to-many  
  - `ICollection<AgentBrand> Brands` — many-to-many  
- [x] **1.4** Create `Models/Identity/Team.cs`  
- [x] **1.5** Create `Models/Identity/AgentTeam.cs` — join table (AgentId, TeamId)  
- [x] **1.6** Create `Models/Identity/AgentBrand.cs` — join table (AgentId, BrandId)  
- [x] **1.7** Wire up `AppDbContext` in `Program.cs`; configure SQLite connection string in `appsettings.Development.json`  
- [x] **1.8** Add `dotnet ef migrations add InitialCreate` and apply  

### Roles & Seed
- [x] **1.9** Define role constants in `Models/Identity/Roles.cs`  
- [x] **1.10** Create `Data/SeedData.cs` — on first run seed roles, admin, teams  
- [x] **1.11** Call seed in `Program.cs` on startup  

### Auth UI
- [x] **1.12** Scaffold Identity login/logout pages into `Areas/Identity/`  
- [x] **1.13** Apply the existing layout to Identity pages (`_Layout.cshtml`)  
- [x] **1.14** Protect all controllers with `[Authorize]`; allow anonymous only on login  

### Admin — User & Team Management
- [x] **1.15** `Controllers/AdminController.cs` — restricted to `BrandManager`  
- [x] **1.16** Views: Users/Index, Users/Edit, Teams/Index, Teams/Edit  

**Phase 1 Status:** `[x] IN PROGRESS` → `[x] DONE`

---

## Phase 2 — Email Templates Module ✉️
> Goal: create, manage and copy branded email templates with per-brand variable substitution.

### Data Models
- [x] **2.1** `Models/EmailTemplates/Brand.cs`  
- [x] **2.2** `Models/EmailTemplates/EmailTemplate.cs`  
- [x] **2.3** Add `DbSet<Brand>` and `DbSet<EmailTemplate>` to `AppDbContext`; add migration  

### Service Layer
- [x] **2.4** `Services/EmailTemplateService.cs`  

### Controllers & Views
- [x] **2.5** `Controllers/EmailTemplatesController.cs` — CRUD, restricted to `Admin` + `TeamLeader`  
- [x] **2.6** Views: Index, Create/Edit, Preview, BrandManager  
- [x] **2.7** "Copy HTML" button on Preview page  
- [x] **2.8** ZoHo Signature section on Brand page  

**Phase 2 Status:** `[x] IN PROGRESS` → `[x] DONE`

---

## Phase 3 — Process Templates 📋
> Goal: a library of ready-made process templates agents can browse, preview, and copy to fill in the blanks — client complaints, document review requests, escalations, and similar operational documents. Vacation and schedule requests are **not** process templates — they are managed as a proper approval workflow in the Schedule module (Phase 5).

### Concepts
- A **Process Template** is a structured document with a title, description, a formatted body containing `[BLANK]` placeholders, and optional guidance notes.
- Templates can be **global** (all brands) or scoped to **specific brands**.
- Templates are grouped into **categories** (e.g. Compliance, Client Relations, Internal).
- Agents **copy** the filled-in text to their clipboard or download it as a `.txt` file — no form submission or workflow engine needed at this stage.
- Team leaders / brand managers create and maintain the library; agents are read-only.

### Data Models
- [x] **3.1** `Models/ProcessTemplates/TemplateCategory.cs`
- [x] **3.2** `Models/ProcessTemplates/ProcessTemplate.cs`
- [x] **3.3** `Models/ProcessTemplates/ProcessTemplateBrand.cs` — join table
- [x] **3.4** Add `DbSet<TemplateCategory>`, `DbSet<ProcessTemplate>` to `AppDbContext`; add migration
- [x] **3.5** Seed default categories (`Compliance`, `Client Relations`, `Internal`) and the three built-in templates listed above

### Service Layer
- [x] **3.6** `Services/ProcessTemplateService.cs`

### Controllers & Views
- [x] **3.7** `Controllers/ProcessTemplatesController.cs`
- [x] **3.8** Views: `Index`, `View`, `Create`, `Edit`, `_TemplateCard.cshtml`, `_TemplateForm.cshtml`
- [x] **3.9** `[BLANK]` highlighting — JS replaces tokens with `<span class="template-blank">` on View page

**Phase 3 Status:** `[x] IN PROGRESS` → `[x] DONE`

---

## Phase 4 — Updates / Noticeboard Module 📢
> Goal: team leaders post tagged updates; agents see a deduplicated, easy-to-search feed.

### Data Models
- [x] **4.1** `Models/Updates/Update.cs`  
  ```
  Id, Title, Body (HTML/Markdown),
  AuthorId (FK AppUser), CreatedAt, UpdatedAt,
  IsPinned, IsArchived,
  AffectedBrands (many-to-many → Brand),
  Tags (JSON list of strings)   // e.g. "Singapore", "ID Document", "Link Change"
  ```
- [x] **4.2** `Models/Updates/UpdateBrand.cs` — join table  
- [x] **4.3** Add DbSets + migration  

### Service Layer
- [x] **4.4** `Services/UpdateService.cs`  
  - `GetFeed(brandId?, tag?, searchText?)` — returns newest-first, pinned on top  
  - `PostUpdate(dto)` — creates update, validates at least one brand or "All Brands" flag  
  - `ArchiveOldUpdates(days)` — auto-archive updates older than N days  
  - Conflict detection hint: surface updates sharing the same tag + brand combination for review  

### Controllers & Views
- [x] **4.5** `Controllers/UpdatesController.cs`  
  - `[Authorize(Roles="BrandManager,TeamLeader")]` for `Create`, `Edit`, `Archive`  
  - All roles can view  
- [x] **4.6** Views:  
  - `Feed` (default landing page) — card/timeline layout, sticky filter bar  
    - Filter by: Brand, Tag, Date range, text search  
    - Pinned updates shown at top with badge  
    - Archived toggle  
  - `Create/Edit` — form with Quill editor, brand multi-select, tag input (Tagify), pin toggle  
  - `_UpdateCard.cshtml` partial — reusable card showing brand badges, tags, author, timestamp  
- [x] **4.7** Toast notification on login if there are unread pinned updates since last login  

**Phase 4 Status:** `[x] IN PROGRESS` → `[x] DONE`

---

## Phase 5 — Schedule Manager 🗓️
> Goal: manage agent shifts (fixed + custom), days off, vacation, and a weekend-shift offer wheel. Vacation and schedule-change requests are submitted by agents and **manually reviewed and approved by a team leader** — the schedule is flexible and there is no auto-approval.

### Data Models
- [x] **5.1** `Models/Schedule/ShiftTemplate.cs`  
- [x] **5.2** `Models/Schedule/AgentSchedule.cs`  
- [x] **5.3** `Models/Schedule/WeekendShiftOffer.cs`  
- [x] **5.4** `Models/Schedule/TimeOffRequest.cs`  
- [x] **5.5** Add DbSets + migration  

### Service Layer
- [x] **5.6** `Services/ScheduleService.cs`  

### Controllers & Views
- [x] **5.7** `Controllers/ScheduleController.cs`  
- [x] **5.8** Views:  
  - `WeekView` — weekly grid, click-to-edit modal, pending requests badge  
  - `AgentView` — read-only personal week view  
  - `WeekendWheel` — animated spin selector with offer recording  
  - `MyRequests` — agent request history + submit modal  
  - `ReviewRequests` — leader queue with approve/deny confirmation modal  

**Phase 5 Status:** `[x] IN PROGRESS` → `[x] DONE`

---

## Phase 6 — Performance Reviews 👤
> Goal: team leaders (and BrandManagers) can record structured performance reviews per agent, covering tickets, chats and calls individually. Agents can view their own reviews.

### Data Models
- [x] **6.1** `Models/Performance/PerformanceReview.cs`  
- [x] **6.2** `Models/Performance/ReviewItem.cs`  
- [x] **6.3** Add `DbSet<PerformanceReview>` and `DbSet<ReviewItem>` to `AppDbContext`; add migration  

### Service Layer
- [x] **6.4** `Services/PerformanceService.cs`  

### Controllers & Views
- [x] **6.5** `Controllers/PerformanceController.cs`  
- [x] **6.6** Views: `MyReviews`, `TeamReviews`, `Detail`, `Create`, `Leaderboard`  
- [x] **6.7** Rating inputs — colour-coded 1–10 badge (red/amber/green) in Detail view  

**Phase 6 Status:** `[x] DONE`

---

## Phase 7 — Login Vault 🔑
> Goal: each agent, team leader, and brand manager has a personal credential store

### Security Principles
- Passwords/secrets are **encrypted at rest** using AES-256 via ASP.NET Core `IDataProtector` — never stored as plain text.
- A user can only read their **own** vault entries via the UI.
- TeamLeaders can only write/bulk-provision vault entries for agents **in their teams**.
- BrandManagers can write/bulk-provision for **all** users.
- No vault entry is ever returned to the client in bulk API responses; only one entry is decrypted per explicit user request.

### Data Models
- [x] **7.1** `Models/Vault/VaultCategory.cs`
- [x] **7.2** `Models/Vault/VaultEntry.cs`
- [x] **7.3** `DbSet<VaultCategory>`, `DbSet<VaultEntry>` added; migration applied
- [x] **7.4** Seed: CRM, Quemetrics, Redmine, Call System

### Service Layer
- [x] **7.5** `Services/VaultService.cs` — AES-256 via `IDataProtector`; all CRUD + bulk + audit

### Controllers & Views
- [x] **7.6** `Controllers/VaultController.cs`
- [x] **7.7** Views: `MyVault`, `AddEntry`, `EditEntry`, `BulkProvision`, `BulkUpdatePassword`, `ManageCategories`, `AccessLog`; `Shared/_VaultCard.cshtml`

### UX / Security
- [x] **7.8** Password fields use `type="password"` with show/hide toggle
- [x] **7.9** Copy-to-clipboard with 30 s auto-clear
- [x] **7.10** No DataTables export on vault pages
- [x] **7.11** Every decrypt logged in `VaultAccessLog` (View / Copy)
- [x] **7.12** `VaultAccessLog` model + DbSet + migration
- [x] **7.13** BrandManager can filter access log by user or entry

**Phase 7 Status:** `[x] DONE`

---

## Phase 8 — Weekly / Monthly Reports 📊
> Goal: team leaders submit activity reports; aggregate stats visible to all roles.

### Data Models
- [x] **8.1** `Models/Reports/TeamReport.cs`
- [x] **8.2** `Models/Reports/AgentStat.cs`
- [x] **8.3** `Models/Reports/FTDLanguageStat.cs`
- [x] **8.4** DbSets + `Phase8_Reports` migration applied

### Service Layer
- [x] **8.5** `Services/ReportService.cs` — submit, top-performer flags, FTD-by-language aggregation, highlights (with PerformanceService integration), delete

### Controllers & Views
- [x] **8.6** `Controllers/ReportsController.cs`
- [x] **8.7** Views: `Dashboard` (KPI cards + team grid), `Submit` (dynamic JS agent rows), `Detail` (agent table + ApexCharts FTD bar + CSV export), `Shared/_TeamCard.cshtml` partial

**Phase 8 Status:** `[x] DONE`

---

## Phase 9 — Navigation & UX Polish 🏗️
- [x] **9.1** Update `_Layout.cshtml` sidebar with final nav links and role-gating:  

  | Nav Item | Visible To |
  |----------|------------|
  | Dashboard | All |
  | Updates Feed | All |
  | Process Templates | All (agents read; TL/BrandManager write) |
  | My Vault | All |
  | Email Templates | BrandManager, TeamLeader |
  | Schedule | All (agents see own view only) |
  | Performance Reviews | BrandManager, TeamLeader (write); CSAgent, SAK (own read) |
  | Reports | All |
  | Admin (Users, Teams, Brands, Categories) | BrandManager only |

- [x] **9.2**
  - BrandManager → purple  
  - TeamLeader → blue  
  - SwissArmyKnife → gold ⚔️  
  - CSAgent → grey  
- [x] **9.3**
- [x] **9.4**
- [x] **9.5**
- [x] **9.6**
- [x] **9.7**

**Phase 9 Status:** `[x] IN PROGRESS` → `[x] DONE`

---

## Phase 10 — Testing & Hardening ✅
- [x] **10.1** Add xUnit test project `Unified.Tests`
- [ ] **10.2** Unit tests for `EmailTemplateService` — token substitution, missing token fallback  
- [ ] **10.3** Unit tests for `ProcessTemplateService` — `[BLANK]` token counting, brand-scoped visibility, inactive template exclusion  
- [ ] **10.4** Unit tests for `ScheduleService` — weekend eligibility, spin wheel distribution  
- [ ] **10.5** Unit tests for `ReportService` — top-performer flag computation, FTD aggregation  
- [ ] **10.6** Unit tests for `PerformanceService` — rating range validation, average calculation, top-rated leaderboard  
- [ ] **10.7** Unit tests for `VaultService`:  
  - Encrypt → store → decrypt round-trip returns original password  
  - Agent cannot read another agent's entry (expect `UnauthorizedAccessException`)  
  - TeamLeader bulk provision creates one entry per target user with correct encrypted value  
  - BulkUpdatePassword updates only the password field, preserves all other fields  
  - Custom category creation scoped correctly per role  
- [ ] **10.8** Integration smoke test — app starts, seed runs, login page reachable  
- [ ] **10.9** Role access matrix test — for each of the 4 roles verify allowed and blocked endpoints  
  - CSAgent cannot POST to `/ProcessTemplates/Create`, `/Vault/BulkProvision`, `/Performance/Create`, `/Admin/Users`, `/EmailTemplates/Create`  
  - TeamLeader cannot GET `/Admin/Users`  
  - TeamLeader bulk provision limited to own team members only  
  - SwissArmyKnife appears in all team roster queries  
- [ ] **10.10** Run `dotnet publish` in Release mode — confirm no warnings/errors  

**Phase 10 Status:** `[ ] IN PROGRESS` → `[ ] DONE`

---

## Folder Structure (Target)

```
Unified/
├── Areas/
│   └── Identity/              # Scaffolded Identity UI
├── Controllers/
│   ├── HomeController.cs
│   ├── AdminController.cs     # Users, Teams, Categories (BrandManager only)
│   ├── EmailTemplatesController.cs
│   ├── UpdatesController.cs
│   ├── ScheduleController.cs
│   ├── PerformanceController.cs
│   ├── VaultController.cs
│   ├── ProcessTemplatesController.cs
│   └── ReportsController.cs
├── Data/
│   ├── AppDbContext.cs
│   └── SeedData.cs
├── Models/
│   ├── Identity/              # AppUser, Team, AgentTeam, AgentBrand, Roles
│   ├── EmailTemplates/        # Brand, EmailTemplate
│   ├── Updates/               # Update, UpdateBrand
│   ├── Schedule/              # ShiftTemplate, AgentSchedule, WeekendShiftOffer
│   ├── Performance/           # PerformanceReview, ReviewItem
│   ├── Vault/                 # VaultCategory, VaultEntry, VaultAccessLog
│   ├── ProcessTemplates/      # TemplateCategory, ProcessTemplate, ProcessTemplateBrand
│   └── Reports/               # TeamReport, AgentStat, FTDLanguageStat
├── Services/
│   ├── EmailTemplateService.cs
│   ├── UpdateService.cs
│   ├── ScheduleService.cs
│   ├── PerformanceService.cs
│   ├── VaultService.cs
│   ├── ProcessTemplateService.cs
│   └── ReportService.cs
├── Views/
│   ├── Shared/                # _Layout, _UpdateCard, _TeamCard, _ReviewItemCard, _VaultCard, _TemplateCard, _Toast
│   ├── Home/
│   ├── Admin/                 # Users/Index, Users/Edit, Teams/Index, Teams/Edit
│   ├── EmailTemplates/
│   ├── Updates/
│   ├── Schedule/
│   ├── Performance/           # AgentReviews, Create, MyReviews
│   ├── Vault/                 # MyVault, BulkProvision, BulkUpdatePassword, ManageCategories
│   ├── ProcessTemplates/      # Index, View, Create, Edit
│   └── Reports/
├── wwwroot/
│   ├── assets/
│   │   ├── images/brands/     # brand logos only
│   │   ├── libs/              # trimmed to required libs only (see Phase 0)
│   │   └── scss/
│   ├── css/
│   └── js/
├── Unified.csproj
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```

---

## Key Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Framework | ASP.NET Core 8 MVC | Already in place |
| Database (dev) | SQLite | Zero-config, file-based |
| Database (prod) | SQL Server / PostgreSQL | Connection string swap only |
| ORM | EF Core 8 | Code-first migrations |
| Auth | ASP.NET Identity | Built-in, role-based (4 roles: BrandManager, TeamLeader, CSAgent, SwissArmyKnife) |
| Credential Encryption | ASP.NET Core `IDataProtector` (AES-256) | Built-in, no extra package, keys managed by Data Protection API |
| Rich Text | Quill (already in libs) | Templates & updates editing |
| Calendar | FullCalendar (already in libs) | Schedule week view |
| Charts | ApexCharts (already in libs) | Reports dashboard |
| Tables | DataTables (already in libs) | Templates & reports lists |
| Date Picker | Flatpickr (already in libs) | Schedule & report period |
| Notifications | Toastify-js (already in libs) | In-app toasts |
| Tag Input | @yaireo/tagify (already in libs) | Update tags |
| Drag/Sort | SortableJS (already in libs) | Schedule row ordering |

---

## Agent Instructions

1. **Work one phase at a time.** Do not start Phase N+1 until Phase N is marked `DONE`.  
2. **After each file change, run `dotnet build`** and resolve any errors before continuing.  
3. **Update this file** — check off completed items and update status lines as you go.  
4. **Do not add new NuGet packages** unless specified in the plan; use libs already present in `wwwroot/assets/libs/`.  
5. **All user-facing strings** should be in English only for now; no localisation layer yet.  
6. **Keep controllers thin** — all business logic goes in `Services/`.  
7. **Migrations** — one migration per phase is acceptable; do not squash migrations mid-plan.  
8. **Secrets** — never commit real connection strings; use `appsettings.Development.json` + `dotnet user-secrets` for dev.  
9. **Role names** — always use the constants from `Models/Identity/Roles.cs` (`Roles.BrandManager`, `Roles.TeamLeader`, `Roles.CSAgent`, `Roles.SwissArmyKnife`). Never hard-code role strings.  
10. **SwissArmyKnife agents** — whenever a query filters by team or brand, always include SAK agents as a union; they belong to all teams for scheduling/review/report purposes.  
11. **Brands** — demo brands seeded are `Colbari` and `BullFX`. Additional brands are added through the Admin → Brand Manager UI, not via seed data.  
12. **Performance ratings** — must be validated server-side as integer 1–10; reject anything outside this range with a 400 response.  
13. **Vault encryption** — always use `IDataProtector` injected via `IDataProtectionProvider.CreateProtector("Vault.Credentials.v1")`. Never use a hand-rolled crypto implementation.  
14. **Vault access log** — every call to `VaultService.GetEntry()` must write a `VaultAccessLog` row before returning the decrypted value. This is non-negotiable for audit compliance.  
15. **Vault bulk provision scope** — `VaultService.BulkProvision` must enforce server-side that a TeamLeader's `TargetUserIds` list contains only members of their own teams. Reject with 403 if any ID falls outside scope.  
16. **No vault data in exports** — `ReportsController` and any CSV/print feature must never include vault fields. The `VaultEntry` model must never be referenced from any Report view model.
17. **Process template `[BLANK]` token** — the literal string `[BLANK]` (uppercase, square brackets) is the canonical placeholder. The JS highlighter and the "Insert [BLANK]" toolbar button must both produce exactly this string. Do not use alternative formats like `{{blank}}` or `___`.
18. **Razor comments** — always use `@* comment text *@` for comments in `.cshtml` files. Never use `{{!-- --}}` (Handlebars) or `<!-- -->` (HTML) for code-level comments; these render as visible text or HTML output. `@* *@` is stripped at compile time and never reaches the browser.
