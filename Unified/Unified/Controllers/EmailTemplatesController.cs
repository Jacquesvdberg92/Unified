using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader}")]
public class EmailTemplatesController : Controller
{
    private readonly EmailTemplateService _svc;

    public EmailTemplatesController(EmailTemplateService svc)
    {
        _svc = svc;
    }

    // ── Templates ─────────────────────────────────────────────────────────

    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader},{Roles.CSAgent},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> Index(int? brandId)
    {
        var templates = await _svc.GetAllTemplatesAsync(brandId);
        var brands    = await _svc.GetAllBrandsAsync();
        ViewBag.Brands         = brands;
        ViewBag.SelectedBrandId = brandId;
        return View(templates);
    }

    public IActionResult Create()
    {
        PopulateBrandsList();
        return View(new EmailTemplate());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmailTemplate model)
    {
        if (!ModelState.IsValid) { PopulateBrandsList(); return View(model); }
        await _svc.CreateAsync(model);
        TempData["Success"] = "Template created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var template = await _svc.GetByIdAsync(id);
        if (template == null) return NotFound();
        PopulateBrandsList(template.BrandId);
        return View(template);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EmailTemplate model)
    {
        if (!ModelState.IsValid) { PopulateBrandsList(model.BrandId); return View(model); }
        await _svc.UpdateAsync(model);
        TempData["Success"] = "Template updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteAsync(id);
        TempData["Success"] = "Template deleted.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = $"{Roles.BrandManager},{Roles.TeamLeader},{Roles.CSAgent},{Roles.SwissArmyKnife}")]
    public async Task<IActionResult> Preview(int id, int? brandId)
    {
        var template = await _svc.GetByIdAsync(id);
        if (template == null) return NotFound();

        var renderedHtml = await _svc.RenderPreviewAsync(id, brandId ?? template.BrandId);
        var brands = await _svc.GetAllBrandsAsync();

        ViewBag.RenderedHtml    = renderedHtml;
        ViewBag.Brands          = brands;
        ViewBag.SelectedBrandId = brandId ?? template.BrandId;
        return View(template);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CloneForBrand(int templateId, int brandId)
    {
        var clone = await _svc.CloneForBrandAsync(templateId, brandId);
        TempData["Success"] = "Template cloned for brand.";
        return RedirectToAction(nameof(Edit), new { id = clone.Id });
    }

    // ── Brand Manager ─────────────────────────────────────────────────────

    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> BrandManager()
    {
        var brands = await _svc.GetAllBrandsAsync();
        return View(brands);
    }

    [Authorize(Roles = Roles.BrandManager)]
    public IActionResult CreateBrand() => View(new Brand());

    [Authorize(Roles = Roles.BrandManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBrand(Brand model)
    {
        if (!ModelState.IsValid) return View(model);
        await _svc.CreateBrandAsync(model);
        TempData["Success"] = "Brand created.";
        return RedirectToAction(nameof(BrandManager));
    }

    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> EditBrand(int id)
    {
        var brand = await _svc.GetBrandByIdAsync(id);
        if (brand == null) return NotFound();
        return View(brand);
    }

    [Authorize(Roles = Roles.BrandManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBrand(Brand model)
    {
        if (!ModelState.IsValid) return View(model);
        await _svc.UpdateBrandAsync(model);
        TempData["Success"] = "Brand updated.";
        return RedirectToAction(nameof(BrandManager));
    }

    [Authorize(Roles = Roles.BrandManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBrand(int id)
    {
        await _svc.DeleteBrandAsync(id);
        TempData["Success"] = "Brand deleted.";
        return RedirectToAction(nameof(BrandManager));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void PopulateBrandsList(int? selectedId = null)
    {
        var brands = _svc.GetAllBrandsAsync().GetAwaiter().GetResult();
        ViewBag.Brands         = brands;
        ViewBag.SelectedBrandId = selectedId;
    }
}
