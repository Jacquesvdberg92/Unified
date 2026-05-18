using Unified.Models.EmailTemplates;
using Unified.Models.ProcessTemplates;
using Unified.Services;
using Unified.Tests.Helpers;

namespace Unified.Tests;

public class ProcessTemplateServiceTests
{
    private static async Task<(Unified.Data.AppDbContext db, ProcessTemplateService svc)> BuildAsync(string name)
    {
        var db = DbHelper.CreateInMemory(name);

        var cat = new TemplateCategory { Name = "Compliance", SortOrder = 1 };
        db.TemplateCategories.Add(cat);
        await db.SaveChangesAsync();

        // Two brands
        var b1 = new Brand { Name = "BrandA", BrandLinksJson = "[]" };
        var b2 = new Brand { Name = "BrandB", BrandLinksJson = "[]" };
        db.Brands.AddRange(b1, b2);
        await db.SaveChangesAsync();

        var svc = new ProcessTemplateService(db);

        // Global template (no brand restriction)
        var global = await svc.CreateTemplateAsync(new ProcessTemplate
        {
            Title      = "Global",
            BodyText   = "Please fill [BLANK].",
            CategoryId = cat.Id,
            IsActive   = true
        }, Enumerable.Empty<int>());

        // BrandA-scoped template
        await svc.CreateTemplateAsync(new ProcessTemplate
        {
            Title      = "BrandA Only",
            BodyText   = "[BLANK] for BrandA.",
            CategoryId = cat.Id,
            IsActive   = true
        }, new[] { b1.Id });

        // Inactive template
        await svc.CreateTemplateAsync(new ProcessTemplate
        {
            Title      = "Inactive",
            BodyText   = "Should not appear.",
            CategoryId = cat.Id,
            IsActive   = false
        }, Enumerable.Empty<int>());

        return (db, svc);
    }

    [Fact]
    public async Task GetLibrary_NoBrandFilter_ReturnsOnlyActiveTemplates()
    {
        var (_, svc) = await BuildAsync(nameof(GetLibrary_NoBrandFilter_ReturnsOnlyActiveTemplates));

        var results = await svc.GetLibraryAsync(null, null, null);

        Assert.Equal(2, results.Count);
        Assert.DoesNotContain(results, t => t.Title == "Inactive");
    }

    [Fact]
    public async Task GetLibrary_BrandScoped_ExcludesOtherBrandTemplates()
    {
        var (db, svc) = await BuildAsync(nameof(GetLibrary_BrandScoped_ExcludesOtherBrandTemplates));

        var b2 = db.Brands.First(b => b.Name == "BrandB");
        var results = await svc.GetLibraryAsync(b2.Id, null, null);

        // BrandA Only should be excluded; Global included (no brand restriction)
        Assert.DoesNotContain(results, t => t.Title == "BrandA Only");
        Assert.Contains(results, t => t.Title == "Global");
    }

    [Fact]
    public async Task GetLibrary_BrandA_IncludesBrandAAndGlobal()
    {
        var (db, svc) = await BuildAsync(nameof(GetLibrary_BrandA_IncludesBrandAAndGlobal));

        var b1 = db.Brands.First(b => b.Name == "BrandA");
        var results = await svc.GetLibraryAsync(b1.Id, null, null);

        Assert.Contains(results, t => t.Title == "Global");
        Assert.Contains(results, t => t.Title == "BrandA Only");
    }

    [Fact]
    public async Task Deactivate_HidesTemplateFromLibrary()
    {
        var (db, svc) = await BuildAsync(nameof(Deactivate_HidesTemplateFromLibrary));

        var global = db.ProcessTemplates.First(t => t.Title == "Global");
        await svc.DeactivateTemplateAsync(global.Id);

        var results = await svc.GetLibraryAsync(null, null, null);

        Assert.DoesNotContain(results, t => t.Title == "Global");
    }

    [Fact]
    public async Task BlankTokenCount_IsCorrect()
    {
        var (db, _) = await BuildAsync(nameof(BlankTokenCount_IsCorrect));

        var template = db.ProcessTemplates.First(t => t.Title == "Global");
        var count = System.Text.RegularExpressions.Regex.Matches(template.BodyText, @"\[BLANK\]").Count;

        Assert.Equal(1, count);
    }
}
