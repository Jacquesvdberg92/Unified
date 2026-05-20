using Unified.Models.EmailTemplates;

namespace Unified.Models.Identity;

public class AgentBrand
{
    public string AgentId { get; set; } = string.Empty;
    public AppUser Agent { get; set; } = null!;

    public int BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
}
