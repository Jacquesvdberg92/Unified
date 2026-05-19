// Areas/Identity/Pages/Account/Register.cshtml.cs
#nullable disable
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Unified.Data;
using Unified.Models.Identity;

namespace Unified.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _db;

        public RegisterModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public bool Submitted { get; set; }

        public class InputModel
        {
            [Required, Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Required, EmailAddress]
            public string Email { get; set; }

            [Required, Display(Name = "Requested Role")]
            public string Role { get; set; }

            [Display(Name = "Message / Reason")]
            public string Message { get; set; }
        }

        public void OnGet(string returnUrl = null) => ReturnUrl = returnUrl;

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            _db.AccountRequests.Add(new AccountRequest
            {
                FullName    = Input.FullName,
                Email       = Input.Email,
                Role        = Input.Role,
                Message     = Input.Message,
                RequestedAt = DateTime.UtcNow,
                Status      = AccountRequestStatus.Pending
            });
            await _db.SaveChangesAsync();

            Submitted = true;
            return Page();
        }
    }
}
