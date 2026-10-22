using Unified.Services;

namespace Unified.Tests;

public class HtmlContentTests
{
    [Theory]
    [InlineData("<p>Hi</p><script>alert(1)</script>", "<script")]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\">", "onerror")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript:")]
    [InlineData("<a href=\"data:text/html,<script>alert(1)</script>\">x</a>", "data:text/html")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>", "<iframe")]
    [InlineData("<p style=\"background:url(javascript:alert(1))\">x</p>", "javascript:")]
    [InlineData("<svg><script>alert(1)</script></svg>", "<script")]
    public void Sanitize_RemovesScriptVectors(string input, string forbidden)
    {
        Assert.DoesNotContain(forbidden, HtmlContent.Sanitize(input), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<p class=\"ql-align-center\">Centered</p>")]
    [InlineData("<ol><li data-list=\"bullet\">One</li></ol>")]
    [InlineData("<p><strong>Bold</strong> <em>it</em> <u>u</u> <s>s</s></p>")]
    [InlineData("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer\">link</a>")]
    [InlineData("<a href=\"mailto:support@example.com\">mail</a>")]
    [InlineData("<img src=\"data:image/png;base64,iVBORw0KGgo=\">")]
    public void Sanitize_KeepsEditorFormatting(string input)
    {
        Assert.Equal(input, HtmlContent.Sanitize(input));
    }

    [Fact]
    public void Sanitize_KeepsSignatureTablesAndInlineStyles()
    {
        // Colours are normalised (rgb → rgba) but the styling survives
        var result = HtmlContent.Sanitize("<table><tbody><tr><td style=\"color: #333; font-size: 12px\">Sig</td></tr></tbody></table>");

        Assert.StartsWith("<table><tbody><tr><td style=\"", result);
        Assert.Contains("font-size: 12px", result);
        Assert.Contains("Sig</td>", result);
    }

    [Theory]
    [InlineData("<p>Dear {{Client:Name}}, contact {{Email:Support}}</p>")]
    [InlineData("<a href=\"{{WebsiteUrl:RegionName}}\">site</a>")]
    [InlineData("<a href=\"{{SiteUrl}}/login?ref=email\">login</a>")]
    [InlineData("<a href=\"{{Link:Client Portal}}\">portal</a>")]
    [InlineData("<img src=\"{{Brand:LogoUrl}}\">")]
    public void Sanitize_PreservesTemplateTokens(string input)
    {
        Assert.Equal(input, HtmlContent.Sanitize(input));
    }

    [Fact]
    public void Sanitize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, HtmlContent.Sanitize(null));
        Assert.Equal(string.Empty, HtmlContent.Sanitize(""));
    }
}
