using System.Text.Json;

namespace Unified.Models.EmailTemplates;

public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColour { get; set; }

    // JSON list of { Region, Url }
    public string WebsiteLinksJson { get; set; } = "[]";
    public List<BrandWebsiteLink> GetWebsiteLinks() =>
        JsonSerializer.Deserialize<List<BrandWebsiteLink>>(WebsiteLinksJson) ?? new();

    public string? CrmUrl { get; set; }
    public string? CallSystemUrl { get; set; }
    public string? FooterSignatureHtml { get; set; }
    public string? ZohoSignatureNote { get; set; }
}

public class BrandWebsiteLink
{
    public string Region { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
