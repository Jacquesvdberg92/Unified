namespace Unified.Models.Dashboard;

public class DashboardWidget
{
    public int    Id        { get; set; }
    public string UserId    { get; set; } = string.Empty;
    public string WidgetKey { get; set; } = string.Empty;
    public int    SortOrder { get; set; }
    public int    ColSpan   { get; set; } = 6; // bootstrap col width (e.g. 6 = col-md-6)
}
