using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.ProcessTemplates;

namespace Unified.Services;

public class ProcessTemplateService
{
    private readonly AppDbContext _db;

    public ProcessTemplateService(AppDbContext db)
    {
        _db = db;
    }

    // ── Read ──────────────────────────────────────────────────────────────

    public async Task<List<TemplateCategory>> GetCategoriesAsync()
        => await _db.TemplateCategories.OrderBy(c => c.SortOrder).ToListAsync();

    public async Task<List<ProcessTemplate>> GetLibraryAsync(int? brandId, int? categoryId, string? searchText)
    {
        var query = _db.ProcessTemplates
            .Include(t => t.Category)
            .Include(t => t.AffectedBrands)
            .Where(t => t.IsActive);

        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(searchText))
            query = query.Where(t =>
                t.Title.Contains(searchText) ||
                (t.Description != null && t.Description.Contains(searchText)));

        if (brandId.HasValue)
            query = query.Where(t =>
                !t.AffectedBrands.Any() ||
                t.AffectedBrands.Any(b => b.BrandId == brandId.Value));

        return await query.OrderBy(t => t.CategoryId).ThenBy(t => t.Title).ToListAsync();
    }

    public async Task<ProcessTemplate?> GetTemplateAsync(int id)
        => await _db.ProcessTemplates
            .Include(t => t.Category)
            .Include(t => t.AffectedBrands).ThenInclude(b => b.Brand)
            .Include(t => t.CreatedByUser)
            .FirstOrDefaultAsync(t => t.Id == id);

    // ── Write ─────────────────────────────────────────────────────────────

    public async Task<ProcessTemplate> CreateTemplateAsync(ProcessTemplate template, IEnumerable<int> brandIds)
    {
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;
        _db.ProcessTemplates.Add(template);
        await _db.SaveChangesAsync();
        await SetBrandsAsync(template.Id, brandIds);
        return template;
    }

    public async Task UpdateTemplateAsync(ProcessTemplate template, IEnumerable<int> brandIds)
    {
        template.UpdatedAt = DateTime.UtcNow;
        _db.ProcessTemplates.Update(template);
        await _db.SaveChangesAsync();
        await SetBrandsAsync(template.Id, brandIds);
    }

    public async Task DeactivateTemplateAsync(int id)
    {
        var template = await _db.ProcessTemplates.FindAsync(id);
        if (template is not null)
        {
            template.IsActive = false;
            template.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task SetBrandsAsync(int templateId, IEnumerable<int> brandIds)
    {
        var existing = await _db.ProcessTemplateBrands
            .Where(ptb => ptb.ProcessTemplateId == templateId)
            .ToListAsync();
        _db.ProcessTemplateBrands.RemoveRange(existing);

        foreach (var brandId in brandIds.Distinct())
            _db.ProcessTemplateBrands.Add(new ProcessTemplateBrand
            {
                ProcessTemplateId = templateId,
                BrandId = brandId
            });

        await _db.SaveChangesAsync();
    }
}
