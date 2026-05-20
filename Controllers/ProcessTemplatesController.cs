using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.Identity;
using Unified.Models.ProcessTemplates;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class ProcessTemplatesController : Controller
{
    private readonly ProcessTemplateService _service;
    private readonly AppDbContext _db;

    public ProcessTemplatesController(ProcessTemplateService service, AppDbContext db)
    {
        _service = service;
        _db      = db;
    }

    // GET: /ProcessTemplates
    public async Task<IActionResult> Index(int? brandId, int? categoryId, string? search)
    {
        var templates  = await _service.GetLibraryAsync(brandId, categoryId, search);
        var categories = await _service.GetCategoriesAsync();
        var brands     = await _db.Brands.OrderBy(b => b.Name).ToListAsync();

        ViewBag.Categories       = categories;
        ViewBag.Brands           = brands;
        ViewBag.SelectedBrandId  = brandId;
        ViewBag.SelectedCategory = categoryId;
        ViewBag.Search           = search;

        return View(templates);
    }

    // GET: /ProcessTemplates/View/5
    public async Task<IActionResult> View(int id)
    {
        var template = await _service.GetTemplateAsync(id);
        if (template is null || !template.IsActive) return NotFound();

        var brands = await _db.Brands.OrderBy(b => b.Name).ToListAsync();
        ViewBag.Brands = brands;

        return View(template);
    }

    // GET: /ProcessTemplates/Create
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Create()
    {
        await PopulateViewBagAsync();
        return View(new ProcessTemplate());
    }

    // POST: /ProcessTemplates/Create
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Create(ProcessTemplate template, int[]? brandIds)
    {
        ModelState.Remove(nameof(template.Category));
        ModelState.Remove(nameof(template.CreatedByUser));
        ModelState.Remove(nameof(template.AffectedBrands));

        if (!ModelState.IsValid)
        {
            await PopulateViewBagAsync();
            return View(template);
        }

        template.CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        await _service.CreateTemplateAsync(template, brandIds ?? []);

        TempData["Success"] = $"Template \"{template.Title}\" created.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /ProcessTemplates/Edit/5
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Edit(int id)
    {
        var template = await _service.GetTemplateAsync(id);
        if (template is null) return NotFound();

        await PopulateViewBagAsync(template.AffectedBrands.Select(b => b.BrandId));
        return View(template);
    }

    // POST: /ProcessTemplates/Edit/5
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Edit(int id, ProcessTemplate template, int[]? brandIds)
    {
        if (id != template.Id) return BadRequest();

        ModelState.Remove(nameof(template.Category));
        ModelState.Remove(nameof(template.CreatedByUser));
        ModelState.Remove(nameof(template.AffectedBrands));

        if (!ModelState.IsValid)
        {
            await PopulateViewBagAsync(brandIds?.AsEnumerable() ?? []);
            return View(template);
        }

        await _service.UpdateTemplateAsync(template, brandIds ?? []);

        TempData["Success"] = $"Template \"{template.Title}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /ProcessTemplates/Deactivate/5
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _service.DeactivateTemplateAsync(id);
        TempData["Success"] = "Template deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task PopulateViewBagAsync(IEnumerable<int>? selectedBrandIds = null)
    {
        ViewBag.Categories      = await _service.GetCategoriesAsync();
        ViewBag.Brands          = await _db.Brands.OrderBy(b => b.Name).ToListAsync();
        ViewBag.SelectedBrandIds = selectedBrandIds?.ToList() ?? new List<int>();
    }
}
