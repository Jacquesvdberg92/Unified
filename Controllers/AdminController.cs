using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;

namespace Unified.Controllers;

[Authorize(Roles = Roles.BrandManager)]
public class AdminController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _db;

    public AdminController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
    }

    // ── Index ──────────────────────────────────────────────────────────────

    public IActionResult Index() => RedirectToAction(nameof(Users));

    // ── Users ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> Users(string? search = null, string? filter = null)
    {
        var currentUserId = _userManager.GetUserId(User);
        var pendingAccountRequestsCount = await _db.AccountRequests.CountAsync(r => r.Status == AccountRequestStatus.Pending);

        var users = await _db.Users
            .Include(u => u.Teams).ThenInclude(at => at.Team)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        var userRoles = new Dictionary<string, IList<string>>();
        foreach (var u in users)
            userRoles[u.Id] = await _userManager.GetRolesAsync(u);

        var normalizedSearch = search?.Trim();
        var normalizedFilter = filter?.Trim();

        var filteredUsers = users.Where(user =>
        {
            var role = userRoles[user.Id].FirstOrDefault() ?? string.Empty;
            var teams = string.Join(" ", user.Teams.Select(t => t.Team?.Name ?? string.Empty));
            var haystack = string.Join(' ', user.DisplayName, user.Email, role, teams).ToLowerInvariant();

            var matchesSearch = string.IsNullOrWhiteSpace(normalizedSearch) || haystack.Contains(normalizedSearch.ToLowerInvariant());
            var matchesFilter = string.IsNullOrWhiteSpace(normalizedFilter)
                || normalizedFilter.Equals("all", StringComparison.OrdinalIgnoreCase)
                || normalizedFilter.Equals("internal", StringComparison.OrdinalIgnoreCase) && !user.IsExternal
                || normalizedFilter.Equals("accountmanagers", StringComparison.OrdinalIgnoreCase) && role == Roles.AccountManager
                || normalizedFilter.Equals("cs", StringComparison.OrdinalIgnoreCase) && (role == Roles.CSAgent || role == Roles.TeamLeader || role == Roles.SwissArmyKnife);

            return matchesSearch && matchesFilter;
        }).ToList();

        var allRows = filteredUsers.Select(user =>
        {
            var role = userRoles[user.Id].FirstOrDefault() ?? "—";
            return new AdminUserRowViewModel
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                Role = role,
                Teams = string.Join(", ", user.Teams.Select(t => t.Team?.Name).Where(name => !string.IsNullOrWhiteSpace(name))),
                IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                IsExternal = user.IsExternal,
                CanDelete = user.Id != currentUserId,
                CanLock = user.Id != currentUserId && !(user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow),
                CanUnlock = user.Id != currentUserId && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                IsCurrentUser = user.Id == currentUserId,
                SectionKey = role == Roles.AccountManager ? "account-managers" : (user.IsExternal ? "external" : "internal")
            };
        }).ToList();

        var sections = new List<AdminUserSectionViewModel>
        {
            new()
            {
                Key = "internal",
                Title = "CS / Internal Users",
                Description = "Brand Manager, Team Leader, CS Agent, Finance, and other internal users.",
                Users = allRows.Where(u => u.SectionKey == "internal").ToList()
            },
            new()
            {
                Key = "account-managers",
                Title = "Account Managers",
                Description = "External users who access CS Live Help only.",
                Users = allRows.Where(u => u.SectionKey == "account-managers").ToList()
            },
            new()
            {
                Key = "external",
                Title = "Other External Users",
                Description = "External users that are not Account Managers.",
                Users = allRows.Where(u => u.SectionKey == "external").ToList()
            }
        };

        var model = new AdminUsersIndexViewModel
        {
            SearchTerm = normalizedSearch,
            Filter = normalizedFilter,
            PendingAccountRequestsCount = pendingAccountRequestsCount,
            Sections = sections.Where(s => s.Users.Count > 0 || string.IsNullOrWhiteSpace(normalizedSearch) || string.IsNullOrWhiteSpace(normalizedFilter)).ToList()
        };

        return View("Users/Index", model);
    }

    public IActionResult CreateUser()
    {
        PopulateRolesAndTeams();
        return View("Users/Create");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string displayName, string email, string password,
        string role, int[] teamIds, string? anydeskId)
    {
        if (!ModelState.IsValid) { PopulateRolesAndTeams(); return View(); }

        var user = new AppUser { UserName = email, Email = email, DisplayName = displayName, EmailConfirmed = true, AnydeskId = anydeskId?.Trim() };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            PopulateRolesAndTeams();
            return View("Users/Create");
        }

        await _userManager.AddToRoleAsync(user, role);
        if (role == Roles.SwissArmyKnife) user.IsSwissArmyKnife = true;

        foreach (var tid in teamIds)
            _db.AgentTeams.Add(new AgentTeam { AgentId = user.Id, TeamId = tid });

        await _db.SaveChangesAsync();
        TempData["Success"] = "User created.";
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> EditUser(string id)
    {
        var user = await _db.Users.Include(u => u.Teams).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();
        var roles = await _userManager.GetRolesAsync(user);
        ViewBag.CurrentRole = roles.FirstOrDefault();
        PopulateRolesAndTeams(user.Teams.Select(t => t.TeamId).ToArray());
        return View("Users/Edit", user);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(string id, string displayName, string? language,
        bool hasWeekendShift, bool hasCsLiveHelp, string role, int[] teamIds, string? anydeskId)
    {
        var user = await _db.Users.Include(u => u.Teams).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        user.DisplayName     = displayName;
        user.Language        = language;
        user.HasWeekendShift = hasWeekendShift;
        user.HasCsLiveHelp   = hasCsLiveHelp;
        user.IsSwissArmyKnife = role == Roles.SwissArmyKnife;
        user.AnydeskId       = anydeskId?.Trim();

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);

        _db.AgentTeams.RemoveRange(user.Teams);
        foreach (var tid in teamIds)
            _db.AgentTeams.Add(new AgentTeam { AgentId = user.Id, TeamId = tid });

        await _db.SaveChangesAsync();
        TempData["Success"] = "User updated.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (_userManager.GetUserId(User) == user.Id)
        {
            TempData["Error"] = "You cannot lock your own account.";
            return RedirectToAction(nameof(Users));
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        TempData["Success"] = $"{user.DisplayName} has been locked.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        await _userManager.SetLockoutEndDateAsync(user, null);

        TempData["Success"] = $"{user.DisplayName} has been unlocked.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (_userManager.GetUserId(User) == user.Id)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Users));
        }

        _db.AgentTeams.RemoveRange(_db.AgentTeams.Where(x => x.AgentId == user.Id));
        _db.AgentBrands.RemoveRange(_db.AgentBrands.Where(x => x.AgentId == user.Id));
        _db.TimeOffRequests.RemoveRange(_db.TimeOffRequests.Where(x => x.AgentId == user.Id || x.ReviewedByLeaderId == user.Id));
        _db.AgentSchedules.RemoveRange(_db.AgentSchedules.Where(x => x.AgentId == user.Id));
        _db.PerformanceReviews.RemoveRange(_db.PerformanceReviews.Where(x => x.AgentId == user.Id || x.ReviewedByLeaderId == user.Id));
        _db.VaultEntries.RemoveRange(_db.VaultEntries.Where(x => x.OwnerId == user.Id || x.ProvisionedByUserId == user.Id));
        _db.VaultCategories.RemoveRange(_db.VaultCategories.Where(x => x.CreatedByUserId == user.Id));
        _db.VaultAccessLogs.RemoveRange(_db.VaultAccessLogs.Where(x => x.UserId == user.Id));
        _db.TeamReports.RemoveRange(_db.TeamReports.Where(x => x.ReportedByLeaderId == user.Id));
        _db.AgentStats.RemoveRange(_db.AgentStats.Where(x => x.AgentId == user.Id));
        _db.PoiSimulations.RemoveRange(_db.PoiSimulations.Where(x => x.LoggedById == user.Id || x.ReceivedById == user.Id));
        _db.CsRequests.RemoveRange(_db.CsRequests.Where(x => x.AccountManagerId == user.Id));
        _db.CsRequestComments.RemoveRange(_db.CsRequestComments.Where(x => x.AuthorId == user.Id));
        _db.CsRequestArchives.RemoveRange(_db.CsRequestArchives.Where(x => x.AccountManagerId == user.Id));
        _db.AmAuditLogs.RemoveRange(_db.AmAuditLogs.Where(x => x.UserId == user.Id));
        _db.DashboardWidgets.RemoveRange(_db.DashboardWidgets.Where(x => x.UserId == user.Id));
        _db.WorkDistributions.RemoveRange(_db.WorkDistributions.Where(x => x.CreatedById == user.Id));
        _db.CsLiveAllocationSlots.RemoveRange(_db.CsLiveAllocationSlots.Where(x => x.Agent1Id == user.Id || x.Agent2Id == user.Id || x.CreatedById == user.Id));
        _db.CsLiveAllocationSwapLogs.RemoveRange(_db.CsLiveAllocationSwapLogs.Where(x => x.PreviousAgentId == user.Id || x.NewAgentId == user.Id || x.ChangedById == user.Id));

        await _db.SaveChangesAsync();
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Users));
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"{user.DisplayName} has been deleted.";
        return RedirectToAction(nameof(Users));
    }

    // ── Teams ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> Teams()
    {
        var teams = await _db.Teams
            .Include(t => t.Members).ThenInclude(at => at.Agent)
            .Include(t => t.TeamLeader)
            .ToListAsync();
        return View("Teams/Index", teams);
    }

    public IActionResult CreateTeam()
    {
        PopulateTeamLeaders();
        return View("Teams/Create");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTeam(string name, string? language, string? teamLeaderId)
    {
        var team = new Team { Name = name, Language = language, TeamLeaderId = teamLeaderId };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Team created.";
        return RedirectToAction(nameof(Teams));
    }

    public async Task<IActionResult> EditTeam(int id)
    {
        var team = await _db.Teams.Include(t => t.Members).ThenInclude(at => at.Agent)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();
        PopulateTeamLeaders(team.TeamLeaderId);
        return View("Teams/Edit", team);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTeam(int id, string name, string? language, string? teamLeaderId, string[] memberIds)
    {
        var team = await _db.Teams.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();

        team.Name = name;
        team.Language = language;
        team.TeamLeaderId = teamLeaderId;

        _db.AgentTeams.RemoveRange(team.Members);
        foreach (var uid in memberIds)
            _db.AgentTeams.Add(new AgentTeam { AgentId = uid, TeamId = id });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Team updated.";
        return RedirectToAction(nameof(Teams));
    }

    // ── Account Requests ───────────────────────────────────────────────────

    public async Task<IActionResult> AccountRequests()
    {
        var requests = await _db.AccountRequests
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();
        ViewBag.AllTeams = await _db.Teams.OrderBy(t => t.Name).ToListAsync();
        return View("AccountRequests/Index", requests);
    }

    public async Task<IActionResult> PendingAccountManagers()
    {
        var requests = await _db.AccountRequests
            .Where(r => r.Role == Roles.AccountManager && r.Status == AccountRequestStatus.Pending)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        var usersByEmail = await _userManager.Users
            .Where(u => requests.Select(r => r.Email).Contains(u.Email!))
            .ToDictionaryAsync(u => u.Email!);

        var model = requests.Select(req =>
        {
            usersByEmail.TryGetValue(req.Email, out var user);
            return new PendingAccountManagerViewModel
            {
                AccountRequestId = req.Id,
                FullName = req.FullName,
                Email = req.Email,
                RequestedAt = req.RequestedAt,
                UserId = user?.Id,
                IsLockedOut = user?.LockoutEnd == DateTimeOffset.MaxValue,
                EmailConfirmed = user?.EmailConfirmed ?? false
            };
        }).ToList();

        return View("PendingAccountManagers/Index", model);
    }

    public async Task<IActionResult> ActivityLog(string? user = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 50)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 10 or > 200 ? 50 : pageSize;

        var query = _db.ActivityLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(user))
        {
            query = query.Where(a => (a.UserName ?? string.Empty).Contains(user) || (a.UserId ?? string.Empty).Contains(user));
        }

        if (from.HasValue)
        {
            query = query.Where(a => a.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            var inclusiveTo = to.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(a => a.Timestamp <= inclusiveTo);
        }

        var totalCount = await query.CountAsync();
        var rows = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdminActivityLogRowViewModel
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = a.UserName,
                Action = a.Action,
                Path = a.Path,
                Method = a.Method,
                StatusCode = a.StatusCode,
                DurationMs = a.DurationMs,
                Timestamp = a.Timestamp
            })
            .ToListAsync();

        var model = new AdminActivityLogIndexViewModel
        {
            UserFilter = user,
            FromDate = from,
            ToDate = to,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Rows = rows
        };

        return View("ActivityLog/Index", model);
    }

    public async Task<IActionResult> ErrorLog(int page = 1, int pageSize = 50)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 10 or > 200 ? 50 : pageSize;

        var query = _db.ErrorLogs.AsNoTracking();
        var totalCount = await query.CountAsync();

        var rows = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AdminErrorLogRowViewModel
            {
                Id = e.Id,
                UserId = e.UserId,
                Path = e.Path,
                Method = e.Method,
                ExceptionType = e.ExceptionType,
                Message = e.Message,
                Timestamp = e.Timestamp
            })
            .ToListAsync();

        var model = new AdminErrorLogIndexViewModel
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Rows = rows
        };

        return View("ErrorLog/Index", model);
    }

    public async Task<IActionResult> ErrorLogDetail(long id)
    {
        var row = await _db.ErrorLogs
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new AdminErrorLogDetailViewModel
            {
                Id = e.Id,
                UserId = e.UserId,
                Path = e.Path,
                Method = e.Method,
                ExceptionType = e.ExceptionType,
                Message = e.Message,
                StackTrace = e.StackTrace,
                Timestamp = e.Timestamp
            })
            .FirstOrDefaultAsync();

        if (row == null)
        {
            return NotFound();
        }

        return View("ErrorLog/Detail", row);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRequest(int id, string role, int[] teamIds)
    {
        var req = await _db.AccountRequests.FindAsync(id);
        if (req == null) return NotFound();

        var user = await _userManager.FindByEmailAsync(req.Email);
        var createdUser = false;
        string? tempPassword = null;

        if (user == null)
        {
            user = new AppUser
            {
                UserName = req.Email,
                Email = req.Email,
                DisplayName = req.FullName,
                EmailConfirmed = true
            };

            tempPassword = $"Unified@{Guid.NewGuid().ToString("N")[..8]}!";
            var createResult = await _userManager.CreateAsync(user, tempPassword);
            if (!createResult.Succeeded)
            {
                TempData["Error"] = string.Join("; ", createResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(AccountRequests));
            }

            createdUser = true;
        }
        else
        {
            user.DisplayName = req.FullName;
            user.EmailConfirmed = true;
            await _userManager.SetLockoutEndDateAsync(user, null);
        }

        if (!await _userManager.IsInRoleAsync(user, role))
        {
            await _userManager.AddToRoleAsync(user, role);
        }
        if (role == Roles.SwissArmyKnife) user.IsSwissArmyKnife = true;
        if (role == Roles.AccountManager) user.IsExternal = true;

        if (teamIds?.Length > 0)
        {
            var existingTeamIds = await _db.AgentTeams
                .Where(at => at.AgentId == user.Id)
                .Select(at => at.TeamId)
                .ToListAsync();

            foreach (var tid in teamIds.Where(tid => !existingTeamIds.Contains(tid)))
                _db.AgentTeams.Add(new AgentTeam { AgentId = user.Id, TeamId = tid });
        }

        req.Status = AccountRequestStatus.Approved;
        req.ReviewedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        req.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = createdUser
            ? $"Account created for {req.FullName}. Temporary password: {tempPassword}"
            : $"Account approved for {req.FullName}.";
        return RedirectToAction(nameof(AccountRequests));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequest(int id, string reason)
    {
        var req = await _db.AccountRequests.FindAsync(id);
        if (req == null) return NotFound();

        req.Status          = AccountRequestStatus.Rejected;
        req.ReviewedById    = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        req.ReviewedAt      = DateTime.UtcNow;
        req.RejectionReason = reason;
        await _db.SaveChangesAsync();

        TempData["Info"] = $"Request from {req.FullName} has been rejected.";
        return RedirectToAction(nameof(AccountRequests));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAccountManager(int id)
    {
        var req = await _db.AccountRequests.FirstOrDefaultAsync(r => r.Id == id && r.Role == Roles.AccountManager && r.Status == AccountRequestStatus.Pending);
        if (req == null) return NotFound();

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null)
        {
            TempData["Error"] = "Pending account manager user record was not found.";
            return RedirectToAction(nameof(PendingAccountManagers));
        }

        user.DisplayName = req.FullName;
        user.IsExternal = true;
        user.EmailConfirmed = true;

        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, null);

        if (!await _userManager.IsInRoleAsync(user, Roles.AccountManager))
        {
            await _userManager.AddToRoleAsync(user, Roles.AccountManager);
        }

        req.Status = AccountRequestStatus.Approved;
        req.ReviewedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        req.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Approved Account Manager request for {req.FullName}.";
        return RedirectToAction(nameof(PendingAccountManagers));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectAccountManager(int id)
    {
        var req = await _db.AccountRequests.FirstOrDefaultAsync(r => r.Id == id && r.Role == Roles.AccountManager && r.Status == AccountRequestStatus.Pending);
        if (req == null) return NotFound();

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user != null)
        {
            await _userManager.DeleteAsync(user);
        }

        req.Status = AccountRequestStatus.Rejected;
        req.ReviewedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        req.ReviewedAt = DateTime.UtcNow;
        req.RejectionReason = "Rejected by Management.";

        await _db.SaveChangesAsync();
        TempData["Info"] = $"Rejected Account Manager request for {req.FullName}.";
        return RedirectToAction(nameof(PendingAccountManagers));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    // ── Telegram Bot Settings ───────────────────────────────────────────────

    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> TelegramSettings()
    {
        var settings = await _db.TelegramBotSettings.FirstOrDefaultAsync()
                       ?? new Models.TelegramBotSettings();
        return View("TelegramSettings", settings);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> TelegramSettings(string? botToken, string? chatId, string? isEnabled)
    {
        var settings = await _db.TelegramBotSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new Models.TelegramBotSettings();
            _db.TelegramBotSettings.Add(settings);
        }

        settings.BotToken  = botToken?.Trim();
        settings.ChatId    = chatId?.Trim();
        settings.IsEnabled = isEnabled == "true";
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Telegram settings saved.";
        return RedirectToAction(nameof(TelegramSettings));
    }

    private void PopulateRolesAndTeams(int[]? selectedTeams = null)
    {
        ViewBag.AllRoles = new SelectList(new[]
        {
            Roles.BrandManager, Roles.TeamLeader, Roles.CSAgent,
            Roles.SwissArmyKnife, Roles.AccountManager, Roles.Finance
        });
        ViewBag.AllTeams = _db.Teams.OrderBy(t => t.Name).ToList();
        ViewBag.SelectedTeams = selectedTeams ?? Array.Empty<int>();
    }

    private void PopulateTeamLeaders(string? selectedId = null)
    {
        var leaders = _userManager.Users.OrderBy(u => u.DisplayName).ToList();
        ViewBag.TeamLeaders = new SelectList(leaders, "Id", "DisplayName", selectedId);
    }
}
