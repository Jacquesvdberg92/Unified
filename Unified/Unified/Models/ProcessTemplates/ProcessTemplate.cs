using System.ComponentModel.DataAnnotations;
using Unified.Models.Identity;
using Unified.Models.EmailTemplates;

namespace Unified.Models.ProcessTemplates;

public class ProcessTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public TemplateCategory Category { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public string BodyText { get; set; } = string.Empty;

    public string? GuidanceNotes { get; set; }

    public bool IsActive { get; set; } = true;

    public string? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProcessTemplateBrand> AffectedBrands { get; set; } = new List<ProcessTemplateBrand>();
}
