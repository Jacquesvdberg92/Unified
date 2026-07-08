using Microsoft.AspNetCore.Identity;
using Unified.Models.Identity;

namespace Unified.Middleware;

/// <summary>
/// When Demo:Enabled is true in config, automatically signs every unauthenticated visitor
/// in as the seeded admin user so the demo site requires no login.
/// </summary>
public class DemoAutoLoginMiddleware
{
    private static readonly string[] SkipPrefixes =
        ["/Identity/", "/lib/", "/css/", "/js/", "/assets/", "/favicon", "/hubs/"];

    private readonly RequestDelegate _next;
    private readonly bool _demoEnabled;
    private readonly string _adminEmail;

    public DemoAutoLoginMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next        = next;
        _demoEnabled = configuration["Demo:Enabled"] == "true";
        _adminEmail  = configuration["Seed:AdminEmail"] ?? "admin@unified.local";
    }

    public async Task InvokeAsync(HttpContext context, SignInManager<AppUser> signInManager,
                                  UserManager<AppUser> userManager)
    {
        if (_demoEnabled && !context.User.Identity!.IsAuthenticated && !ShouldSkip(context.Request.Path))
        {
            var user = await userManager.FindByEmailAsync(_adminEmail);
            if (user != null)
            {
                await signInManager.SignInAsync(user, isPersistent: false);
                context.Response.Redirect(context.Request.Path + context.Request.QueryString);
                return;
            }
        }

        await _next(context);
    }

    private static bool ShouldSkip(PathString path)
    {
        foreach (var prefix in SkipPrefixes)
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
