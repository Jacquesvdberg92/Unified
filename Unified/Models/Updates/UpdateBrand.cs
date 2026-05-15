using Unified.Models.EmailTemplates;

namespace Unified.Models.Updates;

public class UpdateBrand
{
    public int UpdateId { get; set; }
    public Update? Update { get; set; }

    public int BrandId { get; set; }
    public Brand? Brand { get; set; }
}
