using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unified.Models.Identity;

namespace Unified.Data;

public static class SeedData
{
    // -- main -------------------------------------------------------------

    public static async Task InitialiseAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var db          = services.GetRequiredService<AppDbContext>();
        var config      = services.GetRequiredService<IConfiguration>();

        // Only migrate if the database needs it
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2714)
        {
            // 2714 = "There is already an object named X in the database"
            // This means migrations were already applied, continue with seeding
            System.Diagnostics.Debug.WriteLine($"Database already initialized: {ex.Message}");
        }

        // -- Roles --------------------------------------------------------
        string[] roles = [Roles.BrandManager, Roles.TeamLeader, Roles.CSAgent, Roles.SwissArmyKnife, Roles.AccountManager, Roles.Finance];
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // -- One-time bootstrap admin (only created when no users exist) --
        if (!userManager.Users.Any())
        {
            var adminEmail    = config["Seed:AdminEmail"]    ?? "admin@unified.local";
            var adminName     = config["Seed:AdminName"]     ?? "Setup Admin";
            var adminPassword = config["Seed:AdminPassword"] ?? "Admin@1234!";

            var admin = new AppUser
            {
                UserName       = adminEmail,
                Email          = adminEmail,
                DisplayName    = adminName,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, Roles.BrandManager);
        }
    }
}
