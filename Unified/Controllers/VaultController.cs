using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.Vault;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class VaultController : Controller
{
    private readonly VaultService         _svc;
    private readonly AppDbContext         _db;
    private readonly UserManager<AppUser> _users;

    public VaultController(VaultService svc, AppDbContext db, UserManager<AppUser> users)
    {
        _svc   = svc;
        _db    = db;
        _users = users;
    }

    // ── My Vault ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> MyVault(int? categoryId)
    {
        var userId  = _users.GetUserId(User)!;
        var entries = await _svc.GetVaultSummariesAsync(userId, categoryId);
        var cats    = await _svc.GetCategoriesAsync();

        ViewBag.Categories  = cats;
        ViewBag.CategoryId  = categoryId;
        return View(entries);
    }

    // ── AJAX: reveal password for ONE entry ───────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reveal(int id, string action = "View")
    {
        var userId = _users.GetUserId(User)!;
        try
        {
            var (_, plain) = await _svc.GetEntryDecryptedAsync(id, userId);
            await _svc.LogAccessAsync(userId, id, action);
            return Json(new { password = plain });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // ── Add / Edit entry ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> AddEntry(int? categoryId)
    {
        ViewBag.Categories = new SelectList(
            await _svc.GetCategoriesAsync(), "Id", "Name", categoryId);
        return View(new VaultEntry { CategoryId = categoryId ?? 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEntry(VaultEntry entry, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
            ModelState.AddModelError("plainPassword", "Password is required.");

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = new SelectList(
                await _svc.GetCategoriesAsync(), "Id", "Name", entry.CategoryId);
            return View(entry);
        }

        var userId = _users.GetUserId(User)!;
        await _svc.UpsertEntryAsync(entry, plainPassword, userId);
        return RedirectToAction(nameof(MyVault));
    }

    [HttpGet]
    public async Task<IActionResult> EditEntry(int id)
    {
        var userId = _users.GetUserId(User)!;
        var entry  = (await _svc.GetVaultForUserAsync(userId)).FirstOrDefault(e => e.Id == id);
        if (entry is null) return NotFound();

        ViewBag.Categories = new SelectList(
            await _svc.GetCategoriesAsync(), "Id", "Name", entry.CategoryId);
        return View(entry);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEntry(VaultEntry entry, string? plainPassword)
    {
        var userId = _users.GetUserId(User)!;

        // If password left blank, keep the existing one
        if (string.IsNullOrWhiteSpace(plainPassword))
        {
            var existing = (await _svc.GetVaultForUserAsync(userId)).FirstOrDefault(e => e.Id == entry.Id);
            if (existing is null) return NotFound();
            plainPassword = _svc.GetType().GetMethod("_protector") is null
                ? existing.EncryptedPassword  // will be re-set; handled in service
                : existing.EncryptedPassword;
            // Pass existing encrypted value directly — service detects IsProtected
            entry.EncryptedPassword = existing.EncryptedPassword;

            // Re-encrypt the same password: just upsert without changing encrypted field.
            existing.Label      = entry.Label;
            existing.Username   = entry.Username;
            existing.CategoryId = entry.CategoryId;
            existing.Url        = entry.Url;
            existing.Notes      = entry.Notes;
            existing.UpdatedAt  = DateTime.UtcNow;
            _db.VaultEntries.Update(existing);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(MyVault));
        }

        await _svc.UpsertEntryAsync(entry, plainPassword, userId);
        return RedirectToAction(nameof(MyVault));
    }

    // ── Delete entry ──────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntry(int id)
    {
        var userId        = _users.GetUserId(User)!;
        var isBrandManager = User.IsInRole(Roles.BrandManager);
        try
        {
            await _svc.DeleteEntryAsync(id, userId, isBrandManager);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        return RedirectToAction(nameof(MyVault));
    }

    // ── Bulk Provision ────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> BulkProvision()
    {
        var leaderId = _users.GetUserId(User)!;
        ViewBag.Categories = new SelectList(await _svc.GetCategoriesAsync(), "Id", "Name");
        ViewBag.Agents     = await GetAccessibleAgentsAsync(leaderId);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> BulkProvision(
        int categoryId, string label, string username, string plainPassword,
        string? url, string? notes, string[] targetUserIds)
    {
        if (string.IsNullOrWhiteSpace(plainPassword) || !targetUserIds.Any())
        {
            TempData["Error"] = "Password and at least one target user are required.";
            return RedirectToAction(nameof(BulkProvision));
        }

        var leaderId = _users.GetUserId(User)!;
        await _svc.BulkProvisionAsync(categoryId, label, username, plainPassword,
                                      url, notes, targetUserIds, leaderId);

        TempData["Success"] = $"Provisioned vault entry for {targetUserIds.Length} user(s).";
        return RedirectToAction(nameof(BulkProvision));
    }

    // ── Bulk Password Update ──────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> BulkUpdatePassword()
    {
        var leaderId = _users.GetUserId(User)!;
        ViewBag.Categories = new SelectList(await _svc.GetCategoriesAsync(), "Id", "Name");
        ViewBag.Agents     = await GetAccessibleAgentsAsync(leaderId);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.TeamLeader},{Roles.BrandManager}")]
    public async Task<IActionResult> BulkUpdatePassword(
        int categoryId, string label, string newPassword, string[] targetUserIds)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || !targetUserIds.Any())
        {
            TempData["Error"] = "New password and at least one target user are required.";
            return RedirectToAction(nameof(BulkUpdatePassword));
        }

        await _svc.BulkUpdatePasswordAsync(categoryId, label, newPassword, targetUserIds);
        TempData["Success"] = $"Password rotated for {targetUserIds.Length} user(s).";
        return RedirectToAction(nameof(BulkUpdatePassword));
    }

    // ── Manage Categories (BrandManager only) ─────────────────────────────

    [HttpGet]
    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> ManageCategories()
    {
        var cats = await _svc.GetCategoriesAsync(includeCustom: true);
        return View(cats);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> CreateCategory(string name, string icon)
    {
        var userId = _users.GetUserId(User)!;
        await _svc.CreateCustomCategoryAsync(name, icon, userId, isSystemWide: true);
        return RedirectToAction(nameof(ManageCategories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        await _svc.DeleteCategoryAsync(id);
        return RedirectToAction(nameof(ManageCategories));
    }

    // ── Access Log (BrandManager only) ────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> AccessLog(string? userId, int? entryId)
    {
        var logs = await _svc.GetAccessLogsAsync(userId, entryId);

        var users = await _db.Users.OrderBy(u => u.DisplayName ?? u.UserName)
            .Select(u => new { u.Id, Name = u.DisplayName ?? u.UserName })
            .ToListAsync();

        ViewBag.Users   = new SelectList(users, "Id", "Name", userId);
        ViewBag.UserId  = userId;
        ViewBag.EntryId = entryId;
        return View(logs);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<List<(string Id, string Name)>> GetAccessibleAgentsAsync(string leaderId)
    {
        IQueryable<AppUser> query;

        if (User.IsInRole(Roles.BrandManager))
        {
            query = _db.Users;
        }
        else
        {
            var teamIds = await _db.AgentTeams
                .Where(at => at.AgentId == leaderId)
                .Select(at => at.TeamId)
                .ToListAsync();

            var agentIds = await _db.AgentTeams
                .Where(at => teamIds.Contains(at.TeamId))
                .Select(at => at.AgentId)
                .Distinct()
                .ToListAsync();

            var sakIds = await _db.Users
                .Where(u => u.IsSwissArmyKnife)
                .Select(u => u.Id)
                .ToListAsync();

            var allIds = agentIds.Union(sakIds).Distinct().ToList();
            query = _db.Users.Where(u => allIds.Contains(u.Id));
        }

        return await query
            .OrderBy(u => u.DisplayName ?? u.UserName)
            .Select(u => new { u.Id, Name = u.DisplayName ?? u.UserName ?? u.Id })
            .AsEnumerable()
            .Select(u => (u.Id, u.Name))
            .ToList()
            .AsTask()
            .ContinueWith(t => t.Result);
    }
}

// Extension to allow .ToList().AsTask()
internal static class ListExtensions
{
    internal static System.Threading.Tasks.Task<List<T>> AsTask<T>(this List<T> list)
        => System.Threading.Tasks.Task.FromResult(list);
}
