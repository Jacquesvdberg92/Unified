using Unified.Models.EmailTemplates;

namespace Unified.Models.ProcessTemplates;

public class ProcessTemplateBrand
{
    public int ProcessTemplateId { get; set; }
    public ProcessTemplate ProcessTemplate { get; set; } = null!;

    public int BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
}
