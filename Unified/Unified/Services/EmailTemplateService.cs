using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Unified.Data;
using Unified.Models.EmailTemplates;

namespace Unified.Services;

public class EmailTemplateService
{
    private readonly AppDbContext _db;

    public EmailTemplateService(AppDbContext db)
    {
        _db = db;
    }

    // Returns master templates (no brand assigned)
    public async Task<List<EmailTemplate>> GetMasterTemplatesAsync()
    {
        return await _db.EmailTemplates
            .Where(t => t.BrandId == null && t.IsActive)
            .OrderBy(t => t.Title)
            .ToListAsync();
    }

    // Returns all templates, optionally filtered by brand
    public async Task<List<EmailTemplate>> GetAllTemplatesAsync(int? brandId = null)
    {
        var query = _db.EmailTemplates
            .Include(t => t.Brand)
            .Where(t => t.IsActive);

        if (brandId.HasValue)
            query = query.Where(t => t.BrandId == brandId || t.BrandId == null);

        return await query.OrderBy(t => t.BrandId).ThenBy(t => t.Title).ToListAsync();
    }

    public async Task<EmailTemplate?> GetByIdAsync(int id)
    {
        return await _db.EmailTemplates.Include(t => t.Brand).FirstOrDefaultAsync(t => t.Id == id);
    }

    // Clone a master template for a specific brand, substituting all tokens
    public async Task<EmailTemplate> CloneForBrandAsync(int templateId, int brandId)
    {
        var master = await _db.EmailTemplates.FindAsync(templateId)
            ?? throw new InvalidOperationException("Template not found.");
        var brand = await _db.Brands.FindAsync(brandId)
            ?? throw new InvalidOperationException("Brand not found.");

        var clone = new EmailTemplate
        {
            Title       = $"{master.Title} — {brand.Name}",
            SubjectLine = SubstituteTokens(master.SubjectLine, brand),
            BodyHtml    = SubstituteTokens(master.BodyHtml, brand),
            BrandId     = brandId,
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        _db.EmailTemplates.Add(clone);
        await _db.SaveChangesAsync();
        return clone;
    }

    // Returns the URL for a brand link, optionally filtered by label
    public async Task<string?> GetBrandLinkAsync(int brandId, string? label = null)
    {
        var brand = await _db.Brands.FindAsync(brandId);
        if (brand == null) return null;

        var links = brand.GetBrandLinks();
        if (label != null)
        {
            var match = links.FirstOrDefault(l => l.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
            return match?.Url;
        }
        return links.FirstOrDefault()?.Url;
    }

    // Returns fully resolved HTML string for preview
    public async Task<string> RenderPreviewAsync(int templateId, int? brandId = null)
    {
        var template = await _db.EmailTemplates.Include(t => t.Brand).FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new InvalidOperationException("Template not found.");

        Brand? brand = template.Brand;
        if (brandId.HasValue)
            brand = await _db.Brands.FindAsync(brandId) ?? brand;

        if (brand == null)
            return template.BodyHtml;

        return SubstituteTokens(template.BodyHtml, brand);
    }

    // Returns the resolved subject line for copy purposes
    public async Task<string> RenderSubjectAsync(int templateId, int? brandId = null)
    {
        var template = await _db.EmailTemplates.Include(t => t.Brand).FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new InvalidOperationException("Template not found.");

        Brand? brand = template.Brand;
        if (brandId.HasValue)
            brand = await _db.Brands.FindAsync(brandId) ?? brand;

        if (brand == null)
            return template.SubjectLine;

        return SubstituteTokens(template.SubjectLine, brand);
    }

    public async Task<EmailTemplate> CreateAsync(EmailTemplate template)
    {
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;
        _db.EmailTemplates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    public async Task UpdateAsync(EmailTemplate template)
    {
        template.UpdatedAt = DateTime.UtcNow;
        _db.EmailTemplates.Update(template);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var t = await _db.EmailTemplates.FindAsync(id);
        if (t != null)
        {
            t.IsActive = false;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<Brand>> GetAllBrandsAsync()
    {
        return await _db.Brands.OrderBy(b => b.Name).ToListAsync();
    }

    public async Task<Brand?> GetBrandByIdAsync(int id)
    {
        return await _db.Brands.FindAsync(id);
    }

    public async Task<Brand> CreateBrandAsync(Brand brand)
    {
        _db.Brands.Add(brand);
        await _db.SaveChangesAsync();
        return brand;
    }

    public async Task UpdateBrandAsync(Brand brand)
    {
        _db.Brands.Update(brand);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteBrandAsync(int id)
    {
        var b = await _db.Brands.FindAsync(id);
        if (b != null)
        {
            _db.Brands.Remove(b);
            await _db.SaveChangesAsync();
        }
    }

    // ── Token substitution ────────────────────────────────────────────────

    private static string SubstituteTokens(string input, Brand brand)
    {
        var links = brand.GetBrandLinks();
        var firstUrl = links.FirstOrDefault()?.Url ?? string.Empty;

        // Standard fixed tokens
        var result = input
            .Replace("{{BrandName}}",         brand.Name)
            .Replace("{{SiteUrl}}",           brand.SiteUrl         ?? string.Empty)
            .Replace("{{CrmUrl}}",            brand.CrmUrl          ?? string.Empty)
            .Replace("{{RedmineUrl}}",        brand.RedmineUrl      ?? string.Empty)
            .Replace("{{QuemetricsUrl}}",     brand.QuemetricsUrl   ?? string.Empty)
            .Replace("{{Email:Dealing}}",     brand.EmailDealing    ?? string.Empty)
            .Replace("{{Email:AML}}",         brand.EmailAml        ?? string.Empty)
            .Replace("{{Email:Assign}}",      brand.EmailAssign     ?? string.Empty)
            .Replace("{{Email:Demo}}",        brand.EmailDemo       ?? string.Empty)
            .Replace("{{FooterSignature}}",   brand.FooterSignatureHtml ?? string.Empty)
            .Replace("{{ZohoSignature}}",     brand.ZohoSignatureNote   ?? string.Empty)
            // Backward-compat aliases
            .Replace("{{WebsiteUrl}}",        firstUrl)
            .Replace("{{CallSystemUrl}}",     brand.QuemetricsUrl   ?? string.Empty);

        // Dynamic labelled link tokens: {{Link:<label>}}
        // Looks up BrandLinksJson by Label (case-insensitive).
        result = Regex.Replace(result, @"\{\{Link:([^}]+)\}\}", m =>
        {
            var label = m.Groups[1].Value.Trim();
            var match = links.FirstOrDefault(l =>
                l.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
            return match?.Url ?? string.Empty;
        });

        // Backward-compat: {{WebsiteUrl:RegionName}} -> lookup by label
        result = Regex.Replace(result, @"\{\{WebsiteUrl:([^}]+)\}\}", m =>
        {
            var label = m.Groups[1].Value.Trim();
            var match = links.FirstOrDefault(l =>
                l.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
            return match?.Url ?? string.Empty;
        });

        return result;
    }
}
