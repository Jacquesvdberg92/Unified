using System.Text.RegularExpressions;
using Ganss.Xss;

namespace Unified.Services;

/// <summary>
/// Cleans user-authored rich text (update bodies, email templates, brand signatures,
/// process guidance notes) so it is safe to render with Html.Raw or hand to innerHTML.
/// Keeps Quill formatting, tables, inline styles, links and pasted images; strips scripts,
/// event handlers and javascript:/data: links.
/// </summary>
public static class HtmlContent
{
    // Email template URLs may be tokens filled in at render time, e.g. href="{{SiteUrl}}/login"
    private static readonly Regex TokenUrl = new(@"^\s*\{\{[^{}]+\}\}", RegexOptions.Compiled);

    // Configured once, then shared: Sanitize() is thread-safe as long as settings don't change
    private static readonly HtmlSanitizer Sanitizer = Create();

    public static string Sanitize(string? html) =>
        string.IsNullOrWhiteSpace(html) ? html ?? string.Empty : Sanitizer.Sanitize(html);

    private static HtmlSanitizer Create()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedAttributes.Add("class");   // Quill alignment / indent / size classes
        sanitizer.AllowedAttributes.Add("target");
        sanitizer.AllowedAttributes.Add("rel");
        sanitizer.AllowDataAttributes = true;        // Quill 2 lists use data-list="bullet|ordered"
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowedSchemes.Add("tel");
        sanitizer.AllowedSchemes.Add("data");

        sanitizer.FilterUrl += (_, e) =>
        {
            // Inert until substituted; rendered templates are sanitized again after substitution
            if (TokenUrl.IsMatch(e.OriginalUrl))
            {
                e.SanitizedUrl = e.OriginalUrl;
                return;
            }

            // data: URLs are only allowed as image sources (Quill embeds pasted images that way)
            if (e.OriginalUrl.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                && !(e.Tag.NodeName == "IMG" && e.OriginalUrl.TrimStart().StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)))
            {
                e.SanitizedUrl = null;
            }
        };
        return sanitizer;
    }
}
