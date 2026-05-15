using System.Text.Json;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;

namespace Unified.Models.Updates;

public class Update
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public string AuthorId { get; set; } = string.Empty;
    public AppUser? Author { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }

    // JSON array of tag strings e.g. ["Singapore","ID Document"]
    public string TagsJson { get; set; } = "[]";
    public List<string> GetTags() =>
        JsonSerializer.Deserialize<List<string>>(TagsJson) ?? new();
    public void SetTags(IEnumerable<string> tags) =>
        TagsJson = JsonSerializer.Serialize(tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToList());

    public ICollection<UpdateBrand> AffectedBrands { get; set; } = new List<UpdateBrand>();
}
