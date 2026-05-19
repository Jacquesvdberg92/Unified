# Unified — Improvement Plan

## Project Overview
**Unified** is an ASP.NET Core MVC (.NET 8) internal operations dashboard targeting call-centre / BPO environments.
It currently has a clean build (0 errors, 0 warnings) and a SQLite database.

---

## Modules Inventory

| Module | Controller | Service | Views | Status |
|---|---|---|---|---|
| Dashboard | HomeController | — | Home/Index | ⚠️ Placeholder only |
| Updates Feed | UpdatesController | UpdateService | Feed / Detail / Create / Edit | ✅ Working |
| Process Templates | ProcessTemplatesController | ProcessTemplateService | Index / View / Create / Edit | ✅ Working |
| Vault (Credentials) | VaultController | VaultService | MyVault / AddEntry / EditEntry / etc. | ✅ Working |
| Schedule | ScheduleController | ScheduleService | AgentView / WeekView / MyRequests / etc. | ✅ Working |
| Performance Reviews | PerformanceController | PerformanceService | MyReviews / Create / Detail / Leaderboard | ✅ Working |
| Reports | ReportsController | ReportService | Dashboard / Submit / Detail | ✅ Working |
| Email Templates | EmailTemplatesController | EmailTemplateService | Index / Create / Edit / Preview | ✅ Working |
| Attendance | AttendanceController | AttendanceService | Index / Retrospective | ✅ Working |
| Attendance Reports | AttendanceReportController | AttendanceService | Index / Retrospective / Holidays | ✅ Working |
| Admin — Users | AdminController | — | Users/Index / Create / Edit | ✅ Working |
| Admin — Teams | AdminController | — | Teams/Index / Create / Edit | ✅ Working |

---

## Issues & Improvements Needed

### 🔴 Critical / High Priority

1. **`UseStatusCodePagesWithReExecute` registered twice** (`Program.cs` lines 57–58)
   - Duplicate middleware registration — remove the second call.

2. **Dashboard (Home/Index) is a placeholder**
   - Shows only a static welcome card.
   - Should display a role-aware summary: today's attendance status, unread updates count, pending schedule requests, upcoming shifts, recent performance reviews.

3. **No global error/toast feedback pattern**
   - `TempData["Success"]` and `TempData["Error"]` are used inconsistently across controllers.
   - `_Toast.cshtml` partial exists but may not be included in `_Layout.cshtml` for all pages.
   - Standardise: ensure the layout always renders the toast partial after TempData is set.

4. **SQL Server connection string in `Program.cs` but project ships with SQLite DB file (`unified.db`)**
   - `appsettings.json` likely still points to SQL Server (`DefaultConnection`).
   - Either switch to SQLite (`UseSqlite`) or document the SQL Server setup requirement clearly.
   - This will cause a runtime failure on a fresh clone without a local SQL Server instance.

### 🟡 Medium Priority

5. **Dashboard needs real KPI widgets**
   - Attendance clock-in/out status for today.
   - Count of unread / recent Updates.
   - Upcoming schedules (next 7 days).
   - Own performance review scores.
   - Reports submitted this month.

6. **Sidebar does not highlight the active menu item**
   - The `class="side-menu__item"` links have no active-state logic (no `asp-route-*` matching or JS).
   - Add a tag helper or ViewData flag to apply `active` CSS class to the current route.

7. **Attendance module missing `AttendanceStatus` enum usage review**
   - `AttendanceStatus.cs` exists; confirm it is used consistently in `AttendanceService` and views.
   - Late / Absent / Leave statuses may not be surfaced in the agent-facing view.

8. **`Unified\Unified\` nested duplicate folder** 
   - There is a `Unified\Unified\Controllers\` and `Unified\Unified\Services\` path (duplicate nesting).
   - These appear to be stale scaffolded files conflicting with the canonical `Unified\Controllers\` path.
   - Files inside: `EmailTemplatesController`, `ProcessTemplatesController`, their services, and models.
   - Should be removed or merged to avoid confusion and potential routing conflicts.

9. **`Program.cs` references two separate `Program.cs` files**
   - Root `Program.cs` is the real entry point.
   - `Unified\Unified\Program.cs` appears to be a duplicate/leftover — should be removed.

10. **Missing role-based navigation visibility**
	- The sidebar shows all links to all roles.
	- Manager-only items (Admin, Reports, Schedule management, Performance create) should be hidden from agents using `@if (User.IsInRole(...))`.

11. **Reports module: no input validation feedback**
	- `Submit.cshtml` form should show field-level validation messages.

12. **Vault: passwords stored with `DataProtection` — no key persistence configured**
	- `AddDataProtection()` is called without `.PersistKeysTo*()`.
	- On app restart keys are regenerated, making all encrypted vault entries unreadable.
	- Keys should be persisted (file system or DB).

13. **Tests project: only unit tests for services — no controller/integration tests**
	- `Unified.Tests` covers `EmailTemplateService`, `PerformanceService`, `ProcessTemplateService`, `ScheduleService`.
	- No tests for `AttendanceService`, `VaultService`, `ReportService`, or any controller action.

### 🟢 Nice to Have / Polish

14. **Leaderboard page** (`Performance/Leaderboard.cshtml`) — verify it is accessible via sidebar for managers.

15. **Weekend Wheel** (`Schedule/WeekendWheel.cshtml`) — confirm scheduling logic is complete.

16. **Bulk Vault Provision** (`Vault/BulkProvision.cshtml` / `BulkUpdatePassword.cshtml`) — verify these work end-to-end.

17. **`_PinnedUpdatesToast.cshtml`** — confirm pinned updates actually appear on page load for all roles.

18. **Mobile responsiveness** — Sprinto/sidebar template appears mobile-first; confirm hamburger menu works on small screens.

19. **`AttendanceReport/Retrospective`** — payroll summary partial (`_PaySummaryPartial`) should be verified for correct calculations.

20. **Add `.editorconfig`** to enforce code style across contributors.

---

## Recommended Execution Order

| # | Task | Effort |
|---|---|---|
| 1 | Fix duplicate middleware in `Program.cs` | S |
| 2 | Fix/confirm database provider (SQLite vs SQL Server) | S |
| 3 | Remove `Unified\Unified\` duplicate nested folder & files | S |
| 4 | Remove stale `Unified\Unified\Program.cs` | S |
| 5 | Fix DataProtection key persistence for Vault | S |
| 6 | Add active-link highlighting to sidebar | S |
| 7 | Show TempData toasts consistently in `_Layout.cshtml` | S |
| 8 | Add role-based sidebar visibility | M |
| 9 | Build a real Dashboard with KPI widgets | M |
| 10 | Add missing service tests (`AttendanceService`, `VaultService`) | M |
| 11 | Add field validation to Reports/Submit form | S |
| 12 | Review Attendance status usage | M |

---

_Created: $(Get-Date -Format "yyyy-MM-dd")_
