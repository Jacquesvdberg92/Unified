using Unified.Models.EmailTemplates;
using Unified.Services;
using Unified.Tests.Helpers;

namespace Unified.Tests;

public class EmailTemplateServiceTests
{
    // ── Token substitution ─────────────────────────────────────────────────

    [Fact]
    public async Task SubstituteTokens_ReplacesAllKnownTokens()
    {
        var db = DbHelper.CreateInMemory(nameof(SubstituteTokens_ReplacesAllKnownTokens));

        var brand = new Brand
        {
            Name               = "TestBrand",
            CrmUrl             = "https://crm.test",
            CallSystemUrl      = "https://calls.test",
            FooterSignatureHtml = "<p>Footer</p>",
            WebsiteLinksJson   = """[{"Region":"EU","Url":"https://eu.test"}]"""
        };
        db.Brands.Add(brand);

        var template = new EmailTemplate
        {
            Title      = "Master",
            SubjectLine = "Hello {{BrandName}}",
            BodyHtml   = "<p>Visit {{WebsiteUrl}} | CRM {{CrmUrl}} | Calls {{CallSystemUrl}} | {{FooterSignature}} | {{Region}}</p>",
            IsActive   = true
        };
        db.EmailTemplates.Add(template);
        await db.SaveChangesAsync();

        var svc = new EmailTemplateService(db);
        var clone = await svc.CloneForBrandAsync(template.Id, brand.Id);

        Assert.Equal("Hello TestBrand", clone.SubjectLine);
        Assert.Contains("https://eu.test", clone.BodyHtml);
        Assert.Contains("https://crm.test", clone.BodyHtml);
        Assert.Contains("https://calls.test", clone.BodyHtml);
        Assert.Contains("<p>Footer</p>", clone.BodyHtml);
        Assert.Contains("EU", clone.BodyHtml);
    }

    [Fact]
    public async Task CloneForBrand_MissingToken_LeavesEmptyString()
    {
        var db = DbHelper.CreateInMemory(nameof(CloneForBrand_MissingToken_LeavesEmptyString));

        var brand = new Brand
        {
            Name             = "NoUrl",
            CrmUrl           = null,
            WebsiteLinksJson = "[]"
        };
        db.Brands.Add(brand);

        var template = new EmailTemplate
        {
            Title      = "T",
            SubjectLine = "{{BrandName}}",
            BodyHtml   = "CRM: {{CrmUrl}} Web: {{WebsiteUrl}}",
            IsActive   = true
        };
        db.EmailTemplates.Add(template);
        await db.SaveChangesAsync();

        var svc   = new EmailTemplateService(db);
        var clone = await svc.CloneForBrandAsync(template.Id, brand.Id);

        Assert.Contains("CRM: ", clone.BodyHtml);
        Assert.Contains("Web: ", clone.BodyHtml);
        // tokens replaced with empty string — no leftover {{…}}
        Assert.DoesNotContain("{{", clone.BodyHtml);
    }
}
