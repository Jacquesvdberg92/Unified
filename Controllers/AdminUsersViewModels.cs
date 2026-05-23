using Unified.Models.Identity;

namespace Unified.Controllers;

public sealed class AdminUsersIndexViewModel
{
    public string? SearchTerm { get; set; }
    public string? Filter { get; set; }
    public int PendingAccountRequestsCount { get; set; }
    public IReadOnlyList<AdminUserSectionViewModel> Sections { get; set; } = [];
}

public sealed class AdminUserSectionViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<AdminUserRowViewModel> Users { get; set; } = [];
}

public sealed class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Teams { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public bool IsExternal { get; set; }
    public bool CanDelete { get; set; }
    public bool CanLock { get; set; }
    public bool CanUnlock { get; set; }
    public bool IsCurrentUser { get; set; }
    public string SectionKey { get; set; } = string.Empty;
}
