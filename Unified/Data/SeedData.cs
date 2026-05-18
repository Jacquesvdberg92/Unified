using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;
using Unified.Models.ProcessTemplates;

namespace Unified.Data;

public static class SeedData
{
    // ── helpers ──────────────────────────────────────────────────────────

    private static async Task<AppUser> EnsureUser(
        UserManager<AppUser> um, string email, string displayName,
        string password, string role)
    {
        var user = await um.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName       = email,
                Email          = email,
                DisplayName    = displayName,
                EmailConfirmed = true
            };
            var r = await um.CreateAsync(user, password);
            if (r.Succeeded)
                await um.AddToRoleAsync(user, role);
            else
                user = await um.FindByEmailAsync(email) ?? user;
        }
        return user;
    }

    private static async Task<Team> EnsureTeam(AppDbContext db, string name)
    {
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Name == name);
        if (team is null)
        {
            team = new Team { Name = name };
            db.Teams.Add(team);
            await db.SaveChangesAsync();
        }
        return team;
    }

    private static async Task AssignTeam(AppDbContext db, string userId, int teamId)
    {
        if (!await db.AgentTeams.AnyAsync(at => at.AgentId == userId && at.TeamId == teamId))
            db.AgentTeams.Add(new AgentTeam { AgentId = userId, TeamId = teamId });
    }

    // ── main ─────────────────────────────────────────────────────────────

    public static async Task InitialiseAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var db          = services.GetRequiredService<AppDbContext>();
        var config      = services.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync();

        // ── Roles ────────────────────────────────────────────────────────
        string[] roles = [Roles.BrandManager, Roles.TeamLeader, Roles.CSAgent, Roles.SwissArmyKnife];
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // ── Brand Managers ───────────────────────────────────────────────
        var mgr1 = await EnsureUser(userManager, "manager1@unified.local", "Manager One",   "Unified@1234!", Roles.BrandManager);
        var mgr2 = await EnsureUser(userManager, "manager2@unified.local", "Manager Two",   "Unified@1234!", Roles.BrandManager);

        // legacy single-account seeds (keep working)
        var adminEmail = config["Seed:AdminEmail"] ?? "admin@unified.local";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
            await EnsureUser(userManager, adminEmail,
                config["Seed:AdminName"] ?? "Brand Manager",
                config["Seed:AdminPassword"] ?? "Admin@1234!", Roles.BrandManager);

        // ── Teams ────────────────────────────────────────────────────────
        var team1 = await EnsureTeam(db, "Team 1");
        var team2 = await EnsureTeam(db, "Team 2");
        var team3 = await EnsureTeam(db, "Team 3");
        var team4 = await EnsureTeam(db, "Team 4");

        // ── Team Leaders ─────────────────────────────────────────────────
        var ldr1 = await EnsureUser(userManager, "leader1@unified.local", "Leader One",   "Unified@1234!", Roles.TeamLeader);
        var ldr2 = await EnsureUser(userManager, "leader2@unified.local", "Leader Two",   "Unified@1234!", Roles.TeamLeader);
        var ldr3 = await EnsureUser(userManager, "leader3@unified.local", "Leader Three", "Unified@1234!", Roles.TeamLeader);
        var ldr4 = await EnsureUser(userManager, "leader4@unified.local", "Leader Four",  "Unified@1234!", Roles.TeamLeader);

        // legacy single-account seed
        var leaderEmail = config["Seed:LeaderEmail"] ?? "leader@unified.local";
        if (await userManager.FindByEmailAsync(leaderEmail) is null)
            await EnsureUser(userManager, leaderEmail,
                config["Seed:LeaderName"] ?? "Team Leader",
                config["Seed:LeaderPassword"] ?? "Leader@1234!", Roles.TeamLeader);

        // Assign leaders to teams and set as team leader
        async Task SetLeader(Team team, AppUser leader)
        {
            team.TeamLeaderId = leader.Id;
            await AssignTeam(db, leader.Id, team.Id);
        }
        await SetLeader(team1, ldr1);
        await SetLeader(team2, ldr2);
        await SetLeader(team3, ldr3);
        await SetLeader(team4, ldr4);
        await db.SaveChangesAsync();

        // ── CS Agents (15) ───────────────────────────────────────────────
        var agentDefs = new[]
        {
            ("agent01@unified.local",  "Agent One"),
            ("agent02@unified.local",  "Agent Two"),
            ("agent03@unified.local",  "Agent Three"),
            ("agent04@unified.local",  "Agent Four"),
            ("agent05@unified.local",  "Agent Five"),
            ("agent06@unified.local",  "Agent Six"),
            ("agent07@unified.local",  "Agent Seven"),
            ("agent08@unified.local",  "Agent Eight"),
            ("agent09@unified.local",  "Agent Nine"),
            ("agent10@unified.local",  "Agent Ten"),
            ("agent11@unified.local",  "Agent Eleven"),
            ("agent12@unified.local",  "Agent Twelve"),
            ("agent13@unified.local",  "Agent Thirteen"),
            ("agent14@unified.local",  "Agent Fourteen"),
            ("agent15@unified.local",  "Agent Fifteen"),
        };

        // legacy single-account seed
        var agentEmail = config["Seed:AgentEmail"] ?? "agent@unified.local";
        if (await userManager.FindByEmailAsync(agentEmail) is null)
            await EnsureUser(userManager, agentEmail,
                config["Seed:AgentName"] ?? "CS Agent",
                config["Seed:AgentPassword"] ?? "Agent@1234!", Roles.CSAgent);

        var teams = new[] { team1, team2, team3, team4 };
        for (int i = 0; i < agentDefs.Length; i++)
        {
            var (email, name) = agentDefs[i];
            var agent = await EnsureUser(userManager, email, name, "Unified@1234!", Roles.CSAgent);
            await AssignTeam(db, agent.Id, teams[i % teams.Length].Id);
        }
        await db.SaveChangesAsync();

        // ── Brands ───────────────────────────────────────────────────────
        var brandDefs = new[]
        {
            new { Name = "Brand 1", Crm = "https://crm.brand1.test",   Qm = "https://qm.brand1.test"  },
            new { Name = "Brand 2", Crm = "https://crm.brand2.test",   Qm = "https://qm.brand2.test"  },
            new { Name = "Brand 3", Crm = "https://crm.brand3.test",   Qm = "https://qm.brand3.test"  },
            new { Name = "Brand 4", Crm = "https://crm.brand4.test",   Qm = "https://qm.brand4.test"  },
        };
        foreach (var b in brandDefs)
        {
            if (!await db.Brands.AnyAsync(x => x.Name == b.Name))
                db.Brands.Add(new Brand
                {
                    Name           = b.Name,
                    CrmUrl         = b.Crm,
                    QuemetricsUrl  = b.Qm,
                    WebsiteLinksJson = "[]"
                });
        }
        await db.SaveChangesAsync();

        // Seed shift templates
        var shiftDefs = new[]
        {
            // Weekday shifts
            new { Name = "Day Shift   (09:00-18:00)",  Start = new TimeSpan(9,  0, 0), End = new TimeSpan(18, 0, 0), Weekend = false },
            new { Name = "Night Shift (12:00-21:00)",  Start = new TimeSpan(12, 0, 0), End = new TimeSpan(21, 0, 0), Weekend = false },
            new { Name = "Midnight    (00:00-09:00)",  Start = new TimeSpan(0,  0, 0), End = new TimeSpan(9,  0, 0), Weekend = false },
            new { Name = "Early Dawn  (04:00-13:00)",  Start = new TimeSpan(4,  0, 0), End = new TimeSpan(13, 0, 0), Weekend = false },
            new { Name = "Early Start (06:00-15:00)",  Start = new TimeSpan(6,  0, 0), End = new TimeSpan(15, 0, 0), Weekend = false },
            new { Name = "Afternoon   (14:00-23:00)",  Start = new TimeSpan(14, 0, 0), End = new TimeSpan(23, 0, 0), Weekend = false },
            new { Name = "Mid-Day     (15:00-00:00)",  Start = new TimeSpan(15, 0, 0), End = new TimeSpan(0,  0, 0), Weekend = false },
            new { Name = "Evening     (18:00-03:00)",  Start = new TimeSpan(18, 0, 0), End = new TimeSpan(3,  0, 0), Weekend = false },
            // Weekend shifts
            new { Name = "Weekend Morning (08:00-15:00)", Start = new TimeSpan(8,  0, 0), End = new TimeSpan(15, 0, 0), Weekend = true  },
            new { Name = "Weekend Evening (14:00-21:00)", Start = new TimeSpan(14, 0, 0), End = new TimeSpan(21, 0, 0), Weekend = true  },
        };
        foreach (var s in shiftDefs)
        {
            if (!await db.ShiftTemplates.AnyAsync(t => t.Name == s.Name))
                db.ShiftTemplates.Add(new Unified.Models.Schedule.ShiftTemplate
                {
                    Name          = s.Name,
                    StartTime     = s.Start,
                    EndTime       = s.End,
                    IsWeekendShift = s.Weekend
                });
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
                        "[ ] Yes - Details: [BLANK]\n" +
                        "[ ] No\n\n" +
                        "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                        "⚠ Please attach a PDF export of the relevant CRM ticket to this report.",
                    GuidanceNotes =
                        "<p><strong>Tips:</strong></p>" +
                        "<ul>" +
                        "<li>Copy the Client ID directly from the CRM - do not abbreviate.</li>" +
                        "<li>Total Deposits and Withdrawals should be converted to USD at the time of reporting.</li>" +
                        "<li>Be specific in Key Complaints - vague entries will be sent back for revision.</li>" +
                        "<li>If the client has not stated expectations, write <em>None stated</em>.</li>" +
                        "<li>Legal/regulatory threats must always be escalated to the Team Leader immediately.</li>" +
                        "<li>The CRM ticket PDF is mandatory - the report will not be processed without it.</li>" +
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
                        "[ ] Proof of Identity       - [ ] Verified  [ ] Declined  [ ] Pending [BLANK]\n" +
                        "[ ] Proof of Address        - [ ] Verified  [ ] Declined  [ ] Pending [BLANK]\n" +
                        "[ ] Bank Statement          - [ ] Verified  [ ] Declined  [ ] Pending [BLANK]\n" +
                        "[ ] [BLANK]                 - [ ] Verified  [ ] Declined  [ ] Pending [BLANK]\n" +
                        "[ ] [BLANK]                 - [ ] Verified  [ ] Declined  [ ] Pending [BLANK]\n\n" +
                        "MISSING DOCUMENTS\n" +
                        "The following documents are still outstanding:\n" +
                        "• [BLANK]\n" +
                        "• [BLANK]\n\n" +
                        "NOTES\n" +
                        "[BLANK]",
                    GuidanceNotes =
                        "<p><strong>Tips:</strong></p>" +
                        "<ul>" +
                        "<li>Tick only the documents that have actually been received - do not pre-fill.</li>" +
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

        // ── Email Templates ──────────────────────────────────────────────
        if (!await db.EmailTemplates.AnyAsync())
        {
            var brands = await db.Brands.ToListAsync();

            // Master welcome template
            db.EmailTemplates.Add(new EmailTemplate
            {
                Title       = "Welcome Email - Master",
                SubjectLine = "Welcome to {{BrandName}}",
                BodyHtml    =
                    "<p>Dear Client,</p>" +
                    "<p>Welcome to <strong>{{BrandName}}</strong>! We are delighted to have you on board.</p>" +
                    "<p>Your dedicated support team is available to assist you at any time. " +
                    "Please visit our website at {{WebsiteUrl}} for the latest information.</p>" +
                    "<p>If you have any questions, do not hesitate to contact us.</p>" +
                    "{{FooterSignature}}",
                IsActive  = true,
                BrandId   = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            // Master withdrawal delay template
            db.EmailTemplates.Add(new EmailTemplate
            {
                Title       = "Withdrawal Delay Notice - Master",
                SubjectLine = "Update on Your Withdrawal Request",
                BodyHtml    =
                    "<p>Dear Client,</p>" +
                    "<p>We are writing to inform you that your recent withdrawal request is currently being processed.</p>" +
                    "<p>Our team is working diligently to complete this as quickly as possible. " +
                    "If you have any questions, please contact your account manager or reach out via {{WebsiteUrl}}.</p>" +
                    "<p>We apologise for any inconvenience and thank you for your patience.</p>" +
                    "{{FooterSignature}}",
                IsActive  = true,
                BrandId   = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            // Master account verification template
            db.EmailTemplates.Add(new EmailTemplate
            {
                Title       = "Account Verification Request - Master",
                SubjectLine = "Action Required: Verify Your Account",
                BodyHtml    =
                    "<p>Dear Client,</p>" +
                    "<p>To continue using your <strong>{{BrandName}}</strong> account, we require you to " +
                    "complete your identity verification.</p>" +
                    "<p>Please log in to your account at {{WebsiteUrl}} and upload the following documents:</p>" +
                    "<ul><li>Proof of Identity (passport or national ID)</li>" +
                    "<li>Proof of Address (utility bill or bank statement dated within 3 months)</li></ul>" +
                    "<p>If you have already submitted these documents, please disregard this email.</p>" +
                    "{{FooterSignature}}",
                IsActive  = true,
                BrandId   = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            // Per-brand welcome clones
            foreach (var brand in brands)
            {
                db.EmailTemplates.Add(new EmailTemplate
                {
                    Title       = $"Welcome Email - {brand.Name}",
                    SubjectLine = $"Welcome to {brand.Name}",
                    BodyHtml    =
                        $"<p>Dear Client,</p>" +
                        $"<p>Welcome to <strong>{brand.Name}</strong>! We are thrilled to have you with us.</p>" +
                        $"<p>Visit us at {{{{WebsiteUrl}}}} or contact our support team at {{{{CrmUrl}}}}.</p>" +
                        "{{FooterSignature}}",
                    IsActive  = true,
                    BrandId   = brand.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
        }

        // ── Schedule ─────────────────────────────────────────────────────
        if (!await db.AgentSchedules.AnyAsync())
        {
            var dayShift   = await db.ShiftTemplates.FirstOrDefaultAsync(s => s.Name.StartsWith("Day Shift"));
            var nightShift = await db.ShiftTemplates.FirstOrDefaultAsync(s => s.Name.StartsWith("Night Shift"));

            if (dayShift != null && nightShift != null)
            {
                // Get all agents
                var agents = await userManager.GetUsersInRoleAsync(Roles.CSAgent);
                // Align to Monday of the current week
                var today   = DateTime.UtcNow.Date;
                var weekStart = today.AddDays(-(int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1);

                int idx = 0;
                foreach (var agent in agents)
                {
                    var shift = idx % 2 == 0 ? dayShift : nightShift;
                    // Seed Mon-Fri for the current week
                    for (int day = 0; day < 5; day++)
                    {
                        var date = weekStart.AddDays(day);
                        if (!await db.AgentSchedules.AnyAsync(s => s.AgentId == agent.Id && s.Date == date))
                        {
                            db.AgentSchedules.Add(new Unified.Models.Schedule.AgentSchedule
                            {
                                AgentId         = agent.Id,
                                Date            = date,
                                ShiftTemplateId = shift.Id,
                                Type            = Unified.Models.Schedule.ScheduleEntryType.Regular
                            });
                        }
                    }
                    idx++;
                }
                await db.SaveChangesAsync();
            }
        }

        // ── Vault ─────────────────────────────────────────────────────────
        if (!await db.VaultEntries.AnyAsync())
        {
            var vaultSvc    = services.GetRequiredService<Unified.Services.VaultService>();
            var allAgents   = await userManager.GetUsersInRoleAsync(Roles.CSAgent);
            var leaders     = await userManager.GetUsersInRoleAsync(Roles.TeamLeader);
            var allUsers    = allAgents.Concat(leaders).ToList();
            var agentIds    = allUsers.Select(u => u.Id).ToList();

            var provisionerId = (await userManager.GetUsersInRoleAsync(Roles.BrandManager))
                                    .FirstOrDefault()?.Id ?? agentIds.First();

            var crmCat  = await db.VaultCategories.FirstAsync(c => c.Name == "CRM");
            var qmCat   = await db.VaultCategories.FirstAsync(c => c.Name == "Quemetrics");

            // Provision CRM access for all agents/leaders
            await vaultSvc.BulkProvisionAsync(
                categoryId           : crmCat.Id,
                label                : "CRM Login",
                username             : "agent.unified",
                plainPassword        : "CRM@test1234!",
                url                  : "https://crm.test",
                notes                : "Seeded test credentials - change before use.",
                targetUserIds        : agentIds,
                provisionedByUserId  : provisionerId);

            // Provision Quemetrics access for all agents/leaders
            await vaultSvc.BulkProvisionAsync(
                categoryId           : qmCat.Id,
                label                : "Quemetrics Login",
                username             : "agent.unified",
                plainPassword        : "QM@test1234!",
                url                  : "https://quemetrics.test",
                notes                : "Seeded test credentials - change before use.",
                targetUserIds        : agentIds,
                provisionedByUserId  : provisionerId);
        }
    }
}
