using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unified.Models.EmailTemplates;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class EmailTemplatesController : Controller
{
    private readonly EmailTemplateService _service;
    private readonly IWebHostEnvironment  _env;

    public EmailTemplatesController(EmailTemplateService service, IWebHostEnvironment env)
    {
        _service = service;
        _env     = env;
    }

    // GET /EmailTemplates
    public async Task<IActionResult> Index(int? brandId)
    {
        var templates = await _service.GetAllTemplatesAsync(brandId);
        var brands    = await _service.GetAllBrandsAsync();
        ViewBag.Brands          = brands;
        ViewBag.SelectedBrandId = brandId;
        return View(templates);
    }

    // GET /EmailTemplates/Preview/5
    public async Task<IActionResult> Preview(int id, int? brandId)
    {
        var template = await _service.GetByIdAsync(id);
        if (template == null) return NotFound();

        var brands           = await _service.GetAllBrandsAsync();
        var renderedHtml     = await _service.RenderPreviewAsync(id, brandId);
        var renderedSubject  = await _service.RenderSubjectAsync(id, brandId);

        ViewBag.Brands          = brands;
        ViewBag.RenderedHtml    = renderedHtml;
        ViewBag.RenderedSubject = renderedSubject;
        ViewBag.SelectedBrandId = brandId;
        return View(template);
    }

    // GET /EmailTemplates/Create
    [Authorize(Roles = "BrandManager,TeamLeader,SwissArmyKnife")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Brands = await _service.GetAllBrandsAsync();
        return View(new EmailTemplate());
    }

    // POST /EmailTemplates/Create
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "BrandManager,TeamLeader,SwissArmyKnife")]
    public async Task<IActionResult> Create(EmailTemplate model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Brands = await _service.GetAllBrandsAsync();
            return View(model);
        }

        await _service.CreateAsync(model);
        TempData["Success"] = "Template created.";
        return RedirectToAction(nameof(Index));
    }

    // GET /EmailTemplates/Edit/5
    [Authorize(Roles = "BrandManager,TeamLeader,SwissArmyKnife")]
    public async Task<IActionResult> Edit(int id)
    {
        var template = await _service.GetByIdAsync(id);
        if (template == null) return NotFound();
        ViewBag.Brands = await _service.GetAllBrandsAsync();
        return View(template);
    }

    // POST /EmailTemplates/Edit/5
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "BrandManager,TeamLeader,SwissArmyKnife")]
    public async Task<IActionResult> Edit(int id, EmailTemplate model)
    {
        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            ViewBag.Brands = await _service.GetAllBrandsAsync();
            return View(model);
        }

        await _service.UpdateAsync(model);
        TempData["Success"] = "Template updated.";
        return RedirectToAction(nameof(Index));
    }

    // POST /EmailTemplates/Delete/5
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "BrandManager,TeamLeader,SwissArmyKnife")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        TempData["Success"] = "Template deleted.";
        return RedirectToAction(nameof(Index));
    }

    // GET /EmailTemplates/BrandManager
    [Authorize(Roles = "BrandManager")]
    public async Task<IActionResult> BrandManager()
    {
        var brands = await _service.GetAllBrandsAsync();
        var docMap = new Dictionary<int, List<BrandDocument>>();
        foreach (var b in brands)
            docMap[b.Id] = await _service.GetDocumentsAsync(b.Id);
        ViewBag.DocumentMap = docMap;
        return View(brands);
    }

    // GET /EmailTemplates/CreateBrand
    [Authorize(Roles = "BrandManager")]
    public IActionResult CreateBrand() => View(new Brand());

    // POST /EmailTemplates/CreateBrand
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "BrandManager")]
    public async Task<IActionResult> CreateBrand(Brand model)
    {
        if (!ModelState.IsValid) return View(model);
        await _service.CreateBrandAsync(model);
        TempData["Success"] = "Brand created.";
        return RedirectToAction(nameof(BrandManager));
    }

    // GET /EmailTemplates/EditBrand/5
    [Authorize(Roles = "BrandManager")]
    public async Task<IActionResult> EditBrand(int id)
    {
        var brand = await _service.GetBrandByIdAsync(id);
        if (brand == null) return NotFound();
        return View(brand);
    }

    // POST /EmailTemplates/EditBrand/5
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "BrandManager")]
    public async Task<IActionResult> EditBrand(int id, Brand model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        await _service.UpdateBrandAsync(model);
        TempData["Success"] = "Brand updated.";
        return RedirectToAction(nameof(BrandManager));
    }

    // POST /EmailTemplates/DeleteBrand/5
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "BrandManager")]
    public async Task<IActionResult> DeleteBrand(int id)
    {
        await _service.DeleteBrandAsync(id);
        TempData["Success"] = "Brand deleted.";
        return RedirectToAction(nameof(BrandManager));
    }

    // POST /EmailTemplates/UploadDocument
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "BrandManager")]
    public async Task<IActionResult> UploadDocument(int brandId, IFormFile file)
    {
        var (success, error, _) = await _service.UploadDocumentAsync(brandId, file, _env.WebRootPath);
        if (!success)
            TempData["Error"] = error;
        else
            TempData["Success"] = "Document uploaded.";
        return RedirectToAction(nameof(BrandManager));
    }

    // POST /EmailTemplates/DeleteDocument/5
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "BrandManager")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        await _service.DeleteDocumentAsync(id, _env.WebRootPath);
        TempData["Success"] = "Document deleted.";
        return RedirectToAction(nameof(BrandManager));
    }

    // GET /EmailTemplates/DownloadDocument/5
    public async Task<IActionResult> DownloadDocument(int id)
    {
        var (doc, filePath) = await _service.GetDocumentForDownloadAsync(id, _env.WebRootPath);
        if (doc == null || filePath == null) return NotFound();

        var ext         = Path.GetExtension(doc.OriginalName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf"            => "application/pdf",
            ".png"            => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif"            => "image/gif",
            ".txt"            => "text/plain",
            _                 => "application/octet-stream"
        };
        return PhysicalFile(filePath, contentType, doc.OriginalName);
    }
}
