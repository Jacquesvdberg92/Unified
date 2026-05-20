using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;
using Unified.Services;

namespace Unified.Controllers;

[Authorize]
public class BrandsController : Controller
{
    private readonly EmailTemplateService _svc;
    private readonly IWebHostEnvironment  _env;

    public BrandsController(EmailTemplateService svc, IWebHostEnvironment env)
    {
        _svc = svc;
        _env = env;
    }

    // ── Public directory — all authenticated users ─────────────────────

    public async Task<IActionResult> Index()
    {
        var brands = await _svc.GetAllBrandsAsync();
        var docMap = new Dictionary<int, List<BrandDocument>>();
        foreach (var b in brands)
            docMap[b.Id] = await _svc.GetDocumentsAsync(b.Id);
        ViewBag.DocumentMap = docMap;
        return View(brands);
    }

    // ── Brand Manager — BrandManager role only ─────────────────────────

    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> Manager()
    {
        var brands = await _svc.GetAllBrandsAsync();
        var docMap = new Dictionary<int, List<BrandDocument>>();
        foreach (var b in brands)
            docMap[b.Id] = await _svc.GetDocumentsAsync(b.Id);
        ViewBag.DocumentMap = docMap;
        return View(brands);
    }

    [Authorize(Roles = Roles.BrandManager)]
    public IActionResult Create() => View(new Brand());

    [Authorize(Roles = Roles.BrandManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Brand model)
    {
        if (!ModelState.IsValid) return View(model);
        await _svc.CreateBrandAsync(model);
        TempData["Success"] = "Brand created.";
        return RedirectToAction(nameof(Manager));
    }

    [Authorize(Roles = Roles.BrandManager)]
    public async Task<IActionResult> Edit(int id)
    {
        var brand = await _svc.GetBrandByIdAsync(id);
        if (brand == null) return NotFound();
        ViewBag.Documents = await _svc.GetDocumentsAsync(id);
        return View(brand);
    }

    [Authorize(Roles = Roles.BrandManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Brand model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Documents = await _svc.GetDocumentsAsync(model.Id);
            return View(model);
        }
        await _svc.UpdateBrandAsync(model);
        TempData["Success"] = "Brand updated.";
        return RedirectToAction(nameof(Manager));
    }

    [Authorize(Roles = Roles.BrandManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteBrandAsync(id);
        TempData["Success"] = "Brand deleted.";
        return RedirectToAction(nameof(Manager));
    }

    // ── Documents ──────────────────────────────────────────────────────

    [Authorize(Roles = Roles.BrandManager)]
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(31_457_280)]
    public async Task<IActionResult> UploadDocument(int brandId, IFormFile file)
    {
        var (success, error, _) = await _svc.UploadDocumentAsync(brandId, file, _env.WebRootPath);
        if (!success)
            TempData["Error"] = error;
        else
            TempData["Success"] = "Document uploaded successfully.";

        return RedirectToAction(nameof(Edit), new { id = brandId });
    }

    [Authorize(Roles = Roles.BrandManager)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(int docId, int brandId)
    {
        await _svc.DeleteDocumentAsync(docId, _env.WebRootPath);
        TempData["Success"] = "Document removed.";
        return RedirectToAction(nameof(Edit), new { id = brandId });
    }

    public async Task<IActionResult> Download(int docId)
    {
        var (doc, filePath) = await _svc.GetDocumentForDownloadAsync(docId, _env.WebRootPath);
        if (doc == null || filePath == null) return NotFound();

        var contentType = GetContentType(doc.StoredName);
        return PhysicalFile(filePath, contentType, doc.OriginalName);
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf"  => "application/pdf",
            ".doc"  => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls"  => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png"  => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif"  => "image/gif",
            ".txt"  => "text/plain",
            _       => "application/octet-stream"
        };
    }
}
