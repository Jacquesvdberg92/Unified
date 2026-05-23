using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Unified.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RequestAccessModel : PageModel
{
    public void OnGet()
    {
    }
}
