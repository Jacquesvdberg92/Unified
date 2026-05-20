namespace Unified.Models.EmailTemplates;

public class EmailTemplate
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SubjectLine { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;

    // null = master template (not brand-specific)
    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
