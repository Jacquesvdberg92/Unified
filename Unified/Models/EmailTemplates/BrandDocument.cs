namespace Unified.Models.EmailTemplates;

public class BrandDocument
{
    public int      Id           { get; set; }
    public int      BrandId      { get; set; }
    public Brand    Brand        { get; set; } = null!;

    /// <summary>Unique filename used on disk (a GUID-based name to avoid collisions).</summary>
    public string   StoredName   { get; set; } = string.Empty;

    /// <summary>The original filename the user uploaded (shown in the UI).</summary>
    public string   OriginalName { get; set; } = string.Empty;

    public DateTime UploadedAt   { get; set; } = DateTime.UtcNow;
}
