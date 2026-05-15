using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.Updates;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class UpdatesController : Controller
{
    private readonly UpdateService        _service;
    private readonly AppDbContext         _db;
    private readonly UserManager<AppUser> _users;

    public UpdatesController(UpdateService service, AppDbContext db, UserManager<AppUser> users)
    {
        _service = service;
        _db      = db;
        _users   = users;
    }

    // GET: /Updates  (Feed)
    public async Task<IActionResult> Feed(int? brandId, string? tag, string? search, bool archived = false)
    {
        var updates = await _service.GetFeedAsync(brandId, tag, search, archived);
        var brands  = await _db.Brands.OrderBy(b => b.Name).ToListAsync();

        ViewBag.Brands           = brands;
        ViewBag.SelectedBrandId  = brandId;
        ViewBag.SelectedTag      = tag;
        ViewBag.Search           = search;
        ViewBag.ShowArchived     = archived;

        // Collect all unique tags for filter bar
        var allTags = updates
            .SelectMany(u => u.GetTags())
            .Distinct()
            .OrderBy(t => t)
            .ToList();
        ViewBag.AllTags = allTags;

        return View(updates);
    }

    // GET: /Updates/Detail/5
    public async Task<IActionResult> Detail(int id)
    {
        var update = await _service.GetByIdAsync(id);
        if (update is null) return NotFound();
        return View(update);
    }

    // GET: /Updates/Create
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Create()
    {
        await PopulateViewBagAsync();
        return View(new Update());
    }

    // POST: /Updates/Create
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Create(Update update, int[]? brandIds, string? tags)
    {
        ModelState.Remove(nameof(update.Author));
        ModelState.Remove(nameof(update.AffectedBrands));

        if (!ModelState.IsValid)
        {
            await PopulateViewBagAsync();
            return View(update);
        }

        update.AuthorId = _users.GetUserId(User)!;
        update.SetTags(ParseTags(tags));

        await _service.PostUpdateAsync(update, brandIds ?? []);
        TempData["Success"] = $"Update \"{update.Title}\" posted.";
        return RedirectToAction(nameof(Feed));
    }

    // GET: /Updates/Edit/5
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Edit(int id)
    {
        var update = await _service.GetByIdAsync(id);
        if (update is null) return NotFound();
        await PopulateViewBagAsync(update);
        return View(update);
    }

    // POST: /Updates/Edit/5
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Edit(int id, Update update, int[]? brandIds, string? tags)
    {
        if (id != update.Id) return BadRequest();

        ModelState.Remove(nameof(update.Author));
        ModelState.Remove(nameof(update.AffectedBrands));

        if (!ModelState.IsValid)
        {
            await PopulateViewBagAsync(update);
            return View(update);
        }

        update.SetTags(ParseTags(tags));
        await _service.EditUpdateAsync(update, brandIds ?? []);
        TempData["Success"] = $"Update \"{update.Title}\" saved.";
        return RedirectToAction(nameof(Feed));
    }

    // POST: /Updates/Archive/5
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Archive(int id)
    {
        await _service.ArchiveAsync(id);
        TempData["Success"] = "Update archived.";
        return RedirectToAction(nameof(Feed));
    }

    // POST: /Updates/Delete/5
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        TempData["Success"] = "Update deleted.";
        return RedirectToAction(nameof(Feed));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task PopulateViewBagAsync(Update? update = null)
    {
        var brands = await _db.Brands.OrderBy(b => b.Name).ToListAsync();
        ViewBag.Brands = brands;

        if (update is not null)
        {
            var selectedBrandIds = update.AffectedBrands.Select(ab => ab.BrandId).ToList();
            ViewBag.SelectedBrandIds = selectedBrandIds;
            ViewBag.CurrentTags      = string.Join(",", update.GetTags());
        }
    }

    private static IEnumerable<string> ParseTags(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
