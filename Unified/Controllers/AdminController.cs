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

    // ── Users ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> Users()
    {
        var users = await _db.Users
            .Include(u => u.Teams).ThenInclude(at => at.Team)
            .ToListAsync();

        var userRoles = new Dictionary<string, IList<string>>();
        foreach (var u in users)
            userRoles[u.Id] = await _userManager.GetRolesAsync(u);

        ViewBag.UserRoles = userRoles;
        return View("Users/Index", users);
    }

    public IActionResult CreateUser()
    {
        PopulateRolesAndTeams();
        return View("Users/Create");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string displayName, string email, string password,
        string role, int[] teamIds)
    {
        if (!ModelState.IsValid) { PopulateRolesAndTeams(); return View(); }

        var user = new AppUser { UserName = email, Email = email, DisplayName = displayName, EmailConfirmed = true };
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
        bool hasWeekendShift, string role, int[] teamIds)
    {
        var user = await _db.Users.Include(u => u.Teams).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        user.DisplayName    = displayName;
        user.Language       = language;
        user.HasWeekendShift = hasWeekendShift;
        user.IsSwissArmyKnife = role == Roles.SwissArmyKnife;

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

    // ── Helpers ────────────────────────────────────────────────────────────

    private void PopulateRolesAndTeams(int[]? selectedTeams = null)
    {
        ViewBag.AllRoles = new SelectList(new[]
        {
            Roles.BrandManager, Roles.TeamLeader, Roles.CSAgent, Roles.SwissArmyKnife
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
