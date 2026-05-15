using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unified.Models.Identity;

namespace Unified.Data;

public static class SeedData
{
    public static async Task InitialiseAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var db          = services.GetRequiredService<AppDbContext>();
        var config      = services.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync();

        // Seed roles
        string[] roles = [Roles.BrandManager, Roles.TeamLeader, Roles.CSAgent, Roles.SwissArmyKnife];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed default BrandManager account
        var adminEmail    = config["Seed:AdminEmail"]    ?? "admin@unified.local";
        var adminPassword = config["Seed:AdminPassword"] ?? "Admin@1234!";
        var adminName     = config["Seed:AdminName"]     ?? "Brand Manager";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new AppUser
            {
                UserName    = adminEmail,
                Email       = adminEmail,
                DisplayName = adminName,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, Roles.BrandManager);
        }

        // Seed demo teams
        string[] teamNames = ["ENG Team", "JP Team", "PT Team"];
        foreach (var name in teamNames)
        {
            if (!await db.Teams.AnyAsync(t => t.Name == name))
                db.Teams.Add(new Team { Name = name });
        }
        await db.SaveChangesAsync();
    }
}
