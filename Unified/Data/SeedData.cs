using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unified.Models.Identity;
using Unified.Models.ProcessTemplates;

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

        // Seed process template categories
        var categoryDefs = new[]
        {
            new { Name = "Compliance",       Icon = "bx bx-shield-quarter", Sort = 1 },
            new { Name = "Client Relations", Icon = "bx bx-user-voice",     Sort = 2 },
            new { Name = "Internal",         Icon = "bx bx-buildings",      Sort = 3 },
        };

        foreach (var def in categoryDefs)
        {
            if (!await db.TemplateCategories.AnyAsync(c => c.Name == def.Name))
                db.TemplateCategories.Add(new TemplateCategory
                {
                    Name         = def.Name,
                    IconCssClass = def.Icon,
                    SortOrder    = def.Sort
                });
        }
        await db.SaveChangesAsync();

        // Seed built-in process templates (only if none exist yet)
        if (!await db.ProcessTemplates.AnyAsync())
        {
            var clientRelCat  = await db.TemplateCategories.FirstAsync(c => c.Name == "Client Relations");
            var complianceCat = await db.TemplateCategories.FirstAsync(c => c.Name == "Compliance");
            var internalCat   = await db.TemplateCategories.FirstAsync(c => c.Name == "Internal");

            db.ProcessTemplates.AddRange(
                new ProcessTemplate
                {
                    Title       = "Complaint - [Cl ID] [Brand Name] [Lang] [Office]",
                    CategoryId  = clientRelCat.Id,
                    Description = "Standard complaint report.",
                    BodyText    =
                        "COMPLAINT REPORT\n" +
                        "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                        "Client ID   : [BLANK]\n" +
                        "Brand       : [BLANK]\n" +
                        "Office      : [BLANK]\n" +
                        "Date        : [BLANK]\n" +
                        "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                        "FINANCIAL SUMMARY\n" +
                        "Total Deposits (USD) : [BLANK]\n" +
                        "Total Withdrawals (USD) : [BLANK]\n\n" +
                        "KEY COMPLAINTS\n" +
                        "• [BLANK]\n" +
                        "• [BLANK]\n" +
                        "• [BLANK]\n\n" +
                        "  Common examples: Pushed to deposit / Account Manager was rude /\n" +
                        "  Withdrawal delayed and/or cancelled / Funds mismanaged\n\n" +
                        "CLIENT EXPECTATIONS\n" +
                        "[BLANK]\n\n" +
                        "LEGAL / REGULATORY ACTION\n" +
                        "Has the client filed or threatened legal action or a police report?\n" +
                        "[ ] Yes — Details: [BLANK]\n" +
                        "[ ] No\n\n" +
                        "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                        "⚠ Please attach a PDF export of the relevant CRM ticket to this report.",
                    GuidanceNotes =
                        "<p><strong>Tips:</strong></p>" +
                        "<ul>" +
                        "<li>Copy the Client ID directly from the CRM — do not abbreviate.</li>" +
                        "<li>Total Deposits and Withdrawals should be converted to USD at the time of reporting.</li>" +
                        "<li>Be specific in Key Complaints — vague entries will be sent back for revision.</li>" +
                        "<li>If the client has not stated expectations, write <em>None stated</em>.</li>" +
                        "<li>Legal/regulatory threats must always be escalated to the Team Leader immediately.</li>" +
                        "<li>The CRM ticket PDF is mandatory — the report will not be processed without it.</li>" +
                        "</ul>",
                    IsActive = true
                },
                new ProcessTemplate
                {
                    Title       = "Non-Depositor Verification",
                    CategoryId  = complianceCat.Id,
                    Description = "Internal document review request for non-depositing client accounts.",
                    BodyText    =
                        "NON-DEPOSITOR VERIFICATION\n" +
                        "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                        "Client ID       : [BLANK]\n" +
                        "Brand           : [BLANK]\n" +
                        "Account Manager : [BLANK]\n" +
                        "Office          : [BLANK]\n" +
                        "Language        : [BLANK]\n" +
                        "Date            : [BLANK]\n" +
                        "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                        "DOCUMENTS RECEIVED\n" +
                        "[ ] Proof of Identity       — [ ] Verified  [ ] Declined  [ ] Pending [BLANK]\n" +
                        "[ ] Proof of Address        — [ ] Verified  [ ] Declined  [ ] Pending [BLANK]\n" +
                        "[ ] Bank Statement          — [ ] Verified  [ ] Declined  [ ] Pending [BLANK]\n" +
                        "[ ] [BLANK]                 — [ ] Verified  [ ] Declined  [ ] Pending [BLANK]\n" +
                        "[ ] [BLANK]                 — [ ] Verified  [ ] Declined  [ ] Pending [BLANK]\n\n" +
                        "MISSING DOCUMENTS\n" +
                        "The following documents are still outstanding:\n" +
                        "• [BLANK]\n" +
                        "• [BLANK]\n\n" +
                        "NOTES\n" +
                        "[BLANK]",
                    GuidanceNotes =
                        "<p><strong>Tips:</strong></p>" +
                        "<ul>" +
                        "<li>Tick only the documents that have actually been received — do not pre-fill.</li>" +
                        "<li><em>Pending</em> status should include the date the document was requested in the [BLANK] next to it.</li>" +
                        "<li><em>Declined</em> documents must have a reason noted (e.g. expired, poor quality, mismatch).</li>" +
                        "<li>List every outstanding document clearly in the Missing Documents section.</li>" +
                        "<li>Add any additional document rows as needed by duplicating the format above.</li>" +
                        "</ul>",
                    IsActive = true
                },
                new ProcessTemplate
                {
                    Title       = "Vacation Request",
                    CategoryId  = internalCat.Id,
                    Description = "Standard vacation leave request submitted by an agent.",
                    BodyText    =
                        "VACATION REQUEST\n" +
                        "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                        "Agent Name  : [BLANK]\n" +
                        "Team        : [BLANK]\n" +
                        "Start Date  : [BLANK]\n" +
                        "End Date    : [BLANK]\n" +
                        "Total Days  : [BLANK]\n" +
                        "Return Date : [BLANK]\n" +
                        "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                        "Please note that I will be on vacation starting [BLANK] and ending on [BLANK].\n" +
                        "I will return to work on [BLANK].\n\n" +
                        "I have ensured that my pending tasks are handed over to [BLANK] for coverage during my absence.\n\n" +
                        "Best regards,\n" +
                        "[BLANK]",
                    GuidanceNotes =
                        "<p><strong>Tips:</strong></p>" +
                        "<ul>" +
                        "<li>Submit this request at least 5 business days in advance.</li>" +
                        "<li>Total Days should exclude weekends unless your shift pattern includes them.</li>" +
                        "<li>Confirm the handover person with your Team Leader before submitting.</li>" +
                        "<li>Copy the completed request and paste it into the Schedule module request form.</li>" +
                        "</ul>",
                    IsActive = true
                }
            );
            await db.SaveChangesAsync();
        }
    }
}
