using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Unified.Data;
using Unified.Models.Identity;

namespace Unified.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterAccountManagerModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    private const int MaxRequestsPerHour = 3;

    public RegisterAccountManagerModel(UserManager<AppUser> userManager, AppDbContext db, IMemoryCache cache)
    {
        _userManager = userManager;
        _db = db;
        _cache = cache;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var cacheKey = $"am-register:{ipAddress}";
        var requestTimes = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return new List<DateTimeOffset>();
        })!;

        requestTimes.RemoveAll(t => t < DateTimeOffset.UtcNow.AddHours(-1));
        if (requestTimes.Count >= MaxRequestsPerHour)
        {
            ModelState.AddModelError(string.Empty, "Too many registration attempts from this IP address. Please try again later.");
            return Page();
        }

        if (await _userManager.FindByEmailAsync(Input.Email) is not null)
        {
            ModelState.AddModelError(string.Empty, "An account with this email already exists or is pending approval.");
            return Page();
        }

        var user = new AppUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            DisplayName = Input.FullName,
            EmailConfirmed = false,
            IsExternal = true,
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.MaxValue
        };

        var createResult = await _userManager.CreateAsync(user, Input.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        try
        {
            _db.AccountRequests.Add(new AccountRequest
            {
                FullName = Input.FullName,
                Email = Input.Email,
                Role = Roles.AccountManager,
                Message = null,
                Status = AccountRequestStatus.Pending,
                RequestedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch
        {
            await _userManager.DeleteAsync(user);
            throw;
        }

        requestTimes.Add(DateTimeOffset.UtcNow);
        _cache.Set(cacheKey, requestTimes, TimeSpan.FromHours(1));

        return RedirectToPage("./RegistrationPending");
    }
}
