using System.ComponentModel.DataAnnotations;

namespace Unified.Models.ProcessTemplates;

public class TemplateCategory
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? IconCssClass { get; set; }

    public int SortOrder { get; set; }

    public ICollection<ProcessTemplate> ProcessTemplates { get; set; } = new List<ProcessTemplate>();
}
