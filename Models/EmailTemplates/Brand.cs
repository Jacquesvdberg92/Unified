using System.Text.Json;

namespace Unified.Models.EmailTemplates;

public class Brand
{
    public int    Id             { get; set; }
    public string Name           { get; set; } = string.Empty;
    public string? LogoUrl       { get; set; }
    public string? PrimaryColour { get; set; }

    // Primary site URL (global / default)
    public string? SiteUrl       { get; set; }

    // Tool URLs
    public string? CrmUrl        { get; set; }
    public string? RedmineUrl    { get; set; }
    public string? QuemetricsUrl { get; set; }

    // Department email addresses
    public string? EmailDealing  { get; set; }
    public string? EmailAml      { get; set; }
    public string? EmailAssign   { get; set; }
    public string? EmailDemo     { get; set; }

    // Labelled document / regional links  (replaces WebsiteLinksJson)
    // JSON list of { Label, Url }
    // e.g. [{"Label":"Bank Details - EN","Url":"https://..."},...]
    public string BrandLinksJson { get; set; } = "[]";
    public List<BrandLink> GetBrandLinks() =>
        JsonSerializer.Deserialize<List<BrandLink>>(BrandLinksJson) ?? new();

    // ZoHo signature
    public string? FooterSignatureHtml { get; set; }
    public string? ZohoSignatureNote   { get; set; }
}

public class BrandLink
{
    public string Label { get; set; } = string.Empty;  // e.g. "Bank Details - EN"
    public string Url   { get; set; } = string.Empty;
}
