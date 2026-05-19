using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Unified.Models.Attendance;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;
using Unified.Models.Performance;
using Unified.Models.Poi;
using Unified.Models.Reports;
using Unified.Models.Schedule;
using Unified.Models.Updates;
using Unified.Models.Vault;
using Unified.Models.WorkDistribution;

namespace Unified.Data;

/// <summary>
/// Loads realistic demo data for showcase / testing purposes.
/// Safe to call on every startup — skips if 5+ brands already exist.
/// Enable via appsettings:  "Seed": { "LoadDemoData": "true" }
/// </summary>
public static class DemoSeedData
{
    private const string DemoPassword = "Demo@1234!";

    public static async Task LoadAsync(IServiceProvider services)
    {
        var db          = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var dpProvider  = services.GetRequiredService<IDataProtectionProvider>();
        var protector   = dpProvider.CreateProtector("Unified.Vault.v1");

        // ── Guard: skip if already seeded ────────────────────────────────
        if (await db.Brands.CountAsync() >= 5)
        {
            Console.WriteLine("[DemoSeed] Already seeded — skipping.");
            return;
        }

        Console.WriteLine("[DemoSeed] Starting demo data load…");

        var rng = new Random(42); // fixed seed for reproducibility

        // ════════════════════════════════════════════════════════════════
        // 1. BRANDS
        // ════════════════════════════════════════════════════════════════
        var brands = new[]
        {
            new Brand
            {
                Name           = "NovaTrade FX",
                PrimaryColour  = "#1A73E8",
                SiteUrl        = "https://novatradefx.demo",
                CrmUrl         = "https://crm.novatradefx.demo",
                RedmineUrl     = "https://redmine.novatradefx.demo",
                QuemetricsUrl  = "https://quemetrics.novatradefx.demo",
                EmailDealing   = "dealing@novatradefx.demo",
                EmailAml       = "aml@novatradefx.demo",
                EmailAssign    = "assign@novatradefx.demo",
                EmailDemo      = "demo@novatradefx.demo",
                BrandLinksJson = JsonSerializer.Serialize(new[]
                {
                    new { Label = "Bank Details - EN", Url = "https://docs.novatradefx.demo/bank-en" },
                    new { Label = "T&C - EN",          Url = "https://docs.novatradefx.demo/tc-en"   }
                }),
                FooterSignatureHtml  = "<p>NovaTrade FX | <a href='https://novatradefx.demo'>novatradefx.demo</a></p>",
                ZohoSignatureNote    = "NovaTrade FX — Regulated Forex Broker"
            },
            new Brand
            {
                Name           = "ApexMarkets",
                PrimaryColour  = "#E84C4C",
                SiteUrl        = "https://apexmarkets.demo",
                CrmUrl         = "https://crm.apexmarkets.demo",
                RedmineUrl     = "https://redmine.apexmarkets.demo",
                QuemetricsUrl  = "https://quemetrics.apexmarkets.demo",
                EmailDealing   = "dealing@apexmarkets.demo",
                EmailAml       = "aml@apexmarkets.demo",
                EmailAssign    = "assign@apexmarkets.demo",
                EmailDemo      = "demo@apexmarkets.demo",
                BrandLinksJson = JsonSerializer.Serialize(new[]
                {
                    new { Label = "Bank Details - FR", Url = "https://docs.apexmarkets.demo/bank-fr" },
                    new { Label = "T&C - FR",          Url = "https://docs.apexmarkets.demo/tc-fr"   }
                }),
                FooterSignatureHtml  = "<p>ApexMarkets | <a href='https://apexmarkets.demo'>apexmarkets.demo</a></p>",
                ZohoSignatureNote    = "ApexMarkets — Premier Trading Platform"
            },
            new Brand
            {
                Name           = "ZenithCapital",
                PrimaryColour  = "#2ECC71",
                SiteUrl        = "https://zenithcapital.demo",
                CrmUrl         = "https://crm.zenithcapital.demo",
                RedmineUrl     = "https://redmine.zenithcapital.demo",
                QuemetricsUrl  = "https://quemetrics.zenithcapital.demo",
                EmailDealing   = "dealing@zenithcapital.demo",
                EmailAml       = "aml@zenithcapital.demo",
                EmailAssign    = "assign@zenithcapital.demo",
                EmailDemo      = "demo@zenithcapital.demo",
                BrandLinksJson = JsonSerializer.Serialize(new[]
                {
                    new { Label = "Bank Details - ES", Url = "https://docs.zenithcapital.demo/bank-es" },
                    new { Label = "T&C - ES",          Url = "https://docs.zenithcapital.demo/tc-es"   }
                }),
                FooterSignatureHtml  = "<p>ZenithCapital | <a href='https://zenithcapital.demo'>zenithcapital.demo</a></p>",
                ZohoSignatureNote    = "ZenithCapital — Wealth at Your Fingertips"
            },
            new Brand
            {
                Name           = "PrimeVault",
                PrimaryColour  = "#9B59B6",
                SiteUrl        = "https://primevault.demo",
                CrmUrl         = "https://crm.primevault.demo",
                RedmineUrl     = "https://redmine.primevault.demo",
                QuemetricsUrl  = "https://quemetrics.primevault.demo",
                EmailDealing   = "dealing@primevault.demo",
                EmailAml       = "aml@primevault.demo",
                EmailAssign    = "assign@primevault.demo",
                EmailDemo      = "demo@primevault.demo",
                BrandLinksJson = JsonSerializer.Serialize(new[]
                {
                    new { Label = "Bank Details - PT", Url = "https://docs.primevault.demo/bank-pt" },
                    new { Label = "T&C - PT",          Url = "https://docs.primevault.demo/tc-pt"   }
                }),
                FooterSignatureHtml  = "<p>PrimeVault | <a href='https://primevault.demo'>primevault.demo</a></p>",
                ZohoSignatureNote    = "PrimeVault — Secure & Profitable"
            },
            new Brand
            {
                Name           = "SilkRoute Invest",
                PrimaryColour  = "#F39C12",
                SiteUrl        = "https://silkrouteinvest.demo",
                CrmUrl         = "https://crm.silkrouteinvest.demo",
                RedmineUrl     = "https://redmine.silkrouteinvest.demo",
                QuemetricsUrl  = "https://quemetrics.silkrouteinvest.demo",
                EmailDealing   = "dealing@silkrouteinvest.demo",
                EmailAml       = "aml@silkrouteinvest.demo",
                EmailAssign    = "assign@silkrouteinvest.demo",
                EmailDemo      = "demo@silkrouteinvest.demo",
                BrandLinksJson = JsonSerializer.Serialize(new[]
                {
                    new { Label = "Bank Details - AR", Url = "https://docs.silkrouteinvest.demo/bank-ar" },
                    new { Label = "T&C - AR",          Url = "https://docs.silkrouteinvest.demo/tc-ar"   }
                }),
                FooterSignatureHtml  = "<p>SilkRoute Invest | <a href='https://silkrouteinvest.demo'>silkrouteinvest.demo</a></p>",
                ZohoSignatureNote    = "SilkRoute Invest — Connecting Eastern Markets"
            }
        };

        db.Brands.AddRange(brands);
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Brands saved.");

        // ════════════════════════════════════════════════════════════════
        // 2. EMAIL TEMPLATES (one per brand)
        // ════════════════════════════════════════════════════════════════
        foreach (var brand in brands)
        {
            db.EmailTemplates.Add(new EmailTemplate
            {
                Title       = $"{brand.Name} — Client Welcome",
                SubjectLine = $"Welcome to {brand.Name}!",
                BodyHtml    = $"<p>Dear {{{{ClientName}}}},</p><p>Welcome to <strong>{brand.Name}</strong>. Your account is now active.</p><p>If you have any questions please reply to this email.</p>{brand.FooterSignatureHtml}",
                BrandId     = brand.Id,
                IsActive    = true,
                CreatedAt   = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Email templates saved.");

        // ════════════════════════════════════════════════════════════════
        // 3. MANAGERS (2)
        // ════════════════════════════════════════════════════════════════
        var manager1 = await CreateUserAsync(userManager, new AppUser
        {
            UserName       = "sarah.mitchell@unified.local",
            Email          = "sarah.mitchell@unified.local",
            DisplayName    = "Sarah Mitchell",
            Language       = "EN",
            EmailConfirmed = true,
            HourlyRate     = 25.00m
        }, DemoPassword, Roles.BrandManager);

        var manager2 = await CreateUserAsync(userManager, new AppUser
        {
            UserName       = "david.okonkwo@unified.local",
            Email          = "david.okonkwo@unified.local",
            DisplayName    = "David Okonkwo",
            Language       = "EN",
            EmailConfirmed = true,
            HourlyRate     = 25.00m
        }, DemoPassword, Roles.BrandManager);

        Console.WriteLine("[DemoSeed] Managers saved.");

        // ════════════════════════════════════════════════════════════════
        // 4. TEAM LEADERS (5)
        // ════════════════════════════════════════════════════════════════
        var leaderDefs = new[]
        {
            ("james.thornton@unified.local",  "James Thornton",    "EN"),
            ("priya.sharma@unified.local",    "Priya Sharma",      "EN"),
            ("carlos.rivera@unified.local",   "Carlos Rivera",     "EN"),
            ("annika.larsson@unified.local",  "Annika Larsson",    "EN"),
            ("mo.alfarsi@unified.local",      "Mohammed Al-Farsi", "EN")
        };

        var leaders = new List<AppUser>();
        foreach (var (email, name, lang) in leaderDefs)
        {
            var u = await CreateUserAsync(userManager, new AppUser
            {
                UserName       = email,
                Email          = email,
                DisplayName    = name,
                Language       = lang,
                EmailConfirmed = true,
                HourlyRate     = 18.00m,
                HasCsLiveHelp  = true
            }, DemoPassword, Roles.TeamLeader);
            leaders.Add(u);
        }
        Console.WriteLine("[DemoSeed] Team leaders saved.");

        // ════════════════════════════════════════════════════════════════
        // 5. TEAMS (5) — created before agents so we have IDs
        // ════════════════════════════════════════════════════════════════
        var teamDefs = new[]
        {
            ("Team Alpha",   "EN/DE", 0),
            ("Team Beta",    "EN/FR", 1),
            ("Team Gamma",   "EN/ES", 2),
            ("Team Delta",   "EN/PT", 3),
            ("Team Epsilon", "EN/AR", 4)
        };

        var teams = new List<Team>();
        foreach (var (name, lang, idx) in teamDefs)
        {
            var team = new Team { Name = name, Language = lang, TeamLeaderId = leaders[idx].Id };
            db.Teams.Add(team);
            teams.Add(team);
        }
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Teams saved.");

        // ════════════════════════════════════════════════════════════════
        // 6. CS AGENTS (25)
        // ════════════════════════════════════════════════════════════════
        var agentDefs = new[]
        {
            // Team Alpha (idx 0) — NovaTrade FX (brand idx 0)
            ("ethan.collins@unified.local",  "Ethan Collins",    "EN", true,  false, 0, 0),
            ("lena.becker@unified.local",    "Lena Becker",      "DE", false, false, 0, 0),
            ("noah.fischer@unified.local",   "Noah Fischer",     "DE", true,  false, 0, 0),
            ("sofia.mendes@unified.local",   "Sofia Mendes",     "EN", false, false, 0, 0),
            ("omar.hassan@unified.local",    "Omar Hassan",      "EN", true,  false, 0, 0),
            // Team Beta (idx 1) — ApexMarkets (brand idx 1)
            ("isabelle.dupont@unified.local","Isabelle Dupont",  "FR", true,  false, 1, 1),
            ("lucas.martin@unified.local",   "Lucas Martin",     "FR", false, false, 1, 1),
            ("aisha.ndiaye@unified.local",   "Aisha Ndiaye",     "EN", true,  false, 1, 1),
            ("ben.carter@unified.local",     "Ben Carter",       "EN", false, false, 1, 1),
            ("yuki.tanaka@unified.local",    "Yuki Tanaka",      "EN", true,  false, 1, 1),
            // Team Gamma (idx 2) — ZenithCapital (brand idx 2)
            ("elena.vasquez@unified.local",  "Elena Vásquez",    "ES", false, false, 2, 2),
            ("diego.lopez@unified.local",    "Diego López",      "ES", true,  false, 2, 2),
            ("rachel.kim@unified.local",     "Rachel Kim",       "EN", false, false, 2, 2),
            ("tom.bradley@unified.local",    "Tom Bradley",      "EN", true,  false, 2, 2),
            ("nina.johansson@unified.local", "Nina Johansson",   "EN", false, false, 2, 2),
            // Team Delta (idx 3) — PrimeVault (brand idx 3)
            ("victor.santos@unified.local",  "Victor Santos",    "PT", true,  false, 3, 3),
            ("ana.ferreira@unified.local",   "Ana Ferreira",     "PT", false, false, 3, 3),
            ("jack.wilson@unified.local",    "Jack Wilson",      "EN", true,  false, 3, 3),
            ("grace.turner@unified.local",   "Grace Turner",     "EN", false, false, 3, 3),
            ("leon.muller@unified.local",    "Leon Müller",      "EN", true,  false, 3, 3),
            // Team Epsilon (idx 4) — SilkRoute Invest (brand idx 4)
            ("fatima.alzahra@unified.local", "Fatima Al-Zahra",  "AR", false, false, 4, 4),
            ("khalid.mansour@unified.local", "Khalid Mansour",   "AR", true,  false, 4, 4),
            ("emma.clarke@unified.local",    "Emma Clarke",      "EN", false, false, 4, 4),
            ("ryan.obrien@unified.local",    "Ryan O'Brien",     "EN", true,  false, 4, 4),
            ("zara.ahmed@unified.local",     "Zara Ahmed",       "EN", false, false, 4, 4)
        };

        var agents = new List<AppUser>();
        foreach (var (email, name, lang, weekend, csLive, teamIdx, brandIdx) in agentDefs)
        {
            var u = await CreateUserAsync(userManager, new AppUser
            {
                UserName         = email,
                Email            = email,
                DisplayName      = name,
                Language         = lang,
                EmailConfirmed   = true,
                HourlyRate       = Math.Round(10m + (decimal)(rng.NextDouble() * 5), 2),
                HasWeekendShift  = weekend,
                HasCsLiveHelp    = csLive
            }, DemoPassword, Roles.CSAgent);
            agents.Add(u);

            // AgentTeam link
            db.AgentTeams.Add(new AgentTeam { AgentId = u.Id, TeamId = teams[teamIdx].Id });

            // AgentBrand link
            db.AgentBrands.Add(new AgentBrand { AgentId = u.Id, BrandId = brands[brandIdx].Id });
        }

        // Also link leaders to their brand
        for (int i = 0; i < leaders.Count; i++)
        {
            db.AgentBrands.Add(new AgentBrand { AgentId = leaders[i].Id, BrandId = brands[i].Id });
            db.AgentTeams.Add(new AgentTeam  { AgentId = leaders[i].Id, TeamId  = teams[i].Id  });
        }

        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Agents, AgentTeam & AgentBrand links saved.");

        // ════════════════════════════════════════════════════════════════
        // 7. SHIFT TEMPLATES
        // ════════════════════════════════════════════════════════════════
        var shiftMorning = new ShiftTemplate { Name = "Morning Shift",   StartTime = new TimeSpan(7,  0, 0), EndTime = new TimeSpan(15, 0, 0), IsWeekendShift = false };
        var shiftAfter   = new ShiftTemplate { Name = "Afternoon Shift", StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(22, 0, 0), IsWeekendShift = false };
        var shiftWeekend = new ShiftTemplate { Name = "Weekend Shift",   StartTime = new TimeSpan(9,  0, 0), EndTime = new TimeSpan(17, 0, 0), IsWeekendShift = true  };
        db.ShiftTemplates.AddRange(shiftMorning, shiftAfter, shiftWeekend);
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Shift templates saved.");

        // ════════════════════════════════════════════════════════════════
        // 8. AGENT SCHEDULES (4 weeks)
        // ════════════════════════════════════════════════════════════════
        var today    = DateTime.UtcNow.Date;
        var schedStart = today.AddDays(-28);
        var schedules  = new List<AgentSchedule>();

        foreach (var agent in agents.Concat(leaders))
        {
            var useWeekend = agent.HasWeekendShift;
            // Alternate morning/afternoon per agent for variety
            var weekdayShift = (agents.IndexOf(agent) % 2 == 0) ? shiftMorning : shiftAfter;

            for (var d = schedStart; d <= today; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                {
                    if (useWeekend)
                        schedules.Add(new AgentSchedule { AgentId = agent.Id, Date = d, ShiftTemplateId = shiftWeekend.Id, Type = ScheduleEntryType.Regular });
                    continue;
                }

                // ~10% vacation / day off
                var roll = rng.Next(100);
                var type = roll < 5  ? ScheduleEntryType.Vacation :
                           roll < 10 ? ScheduleEntryType.DayOff   :
                                       ScheduleEntryType.Regular;

                schedules.Add(new AgentSchedule
                {
                    AgentId         = agent.Id,
                    Date            = d,
                    ShiftTemplateId = type == ScheduleEntryType.Regular ? weekdayShift.Id : null,
                    Type            = type
                });
            }
        }
        db.AgentSchedules.AddRange(schedules);
        await db.SaveChangesAsync();
        Console.WriteLine($"[DemoSeed] {schedules.Count} schedule entries saved.");

        // ════════════════════════════════════════════════════════════════
        // 9. ATTENDANCE LOGS (last 30 days)
        // ════════════════════════════════════════════════════════════════
        var attendanceLogs = new List<AttendanceLog>();
        var attStart       = today.AddDays(-30);

        foreach (var agent in agents.Concat(leaders))
        {
            var agentShift = (agents.IndexOf(agent) % 2 == 0) ? shiftMorning : shiftAfter;

            for (var d = attStart; d < today; d = d.AddDays(1))
            {
                bool isWeekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;
                if (isWeekend && !agent.HasWeekendShift) continue;

                var roll   = rng.Next(100);
                var status = roll < 3  ? AttendanceStatus.Present : // re-used as absent placeholder
                             roll < 10 ? AttendanceStatus.Present   : // late
                                         AttendanceStatus.Present;

                // true absence — skip adding a log (~3%)
                if (roll < 3) continue;

                var shift  = isWeekend ? shiftWeekend : agentShift;
                var inDrift  = TimeSpan.FromMinutes(roll < 10 ? rng.Next(5, 20) : rng.Next(0, 5));
                var outDrift = TimeSpan.FromMinutes(rng.Next(0, 15));

                var checkIn  = d + shift.StartTime + inDrift;
                var checkOut = d + shift.EndTime   + outDrift;

                var payType  = isWeekend          ? DayPayType.Weekend  :
                               d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday
                                                   ? DayPayType.Weekend  :
                                                     DayPayType.Regular;

                attendanceLogs.Add(new AttendanceLog
                {
                    AgentId      = agent.Id,
                    WorkDate     = d,
                    CheckInTime  = checkIn,
                    CheckOutTime = checkOut,
                    Status       = AttendanceStatus.Present,
                    PayType      = payType
                });
            }
        }
        db.AttendanceLogs.AddRange(attendanceLogs);
        await db.SaveChangesAsync();
        Console.WriteLine($"[DemoSeed] {attendanceLogs.Count} attendance logs saved.");

        // ════════════════════════════════════════════════════════════════
        // 10. TEAM REPORTS (4 weekly + 1 monthly per team)
        // ════════════════════════════════════════════════════════════════
        // agents are grouped by team index (5 per team)
        for (int t = 0; t < teams.Count; t++)
        {
            var leader      = leaders[t];
            var teamAgents  = agents.Skip(t * 5).Take(5).ToList();
            var team        = teams[t];

            // 4 weekly reports
            for (int w = 4; w >= 1; w--)
            {
                var weekStart = today.AddDays(-(w * 7));
                var weekEnd   = weekStart.AddDays(6);
                await AddTeamReport(db, rng, team, leader, teamAgents, PeriodType.Weekly, weekStart, weekEnd);
            }

            // 1 monthly report
            var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            var monthEnd   = monthStart.AddMonths(1).AddDays(-1);
            await AddTeamReport(db, rng, team, leader, teamAgents, PeriodType.Monthly, monthStart, monthEnd);
        }
        Console.WriteLine("[DemoSeed] Team reports saved.");

        // ════════════════════════════════════════════════════════════════
        // 11. PERFORMANCE REVIEWS (2 per agent)
        // ════════════════════════════════════════════════════════════════
        var positives = new[] { "Excellent communication skills", "Consistently meets targets", "Great team player", "Fast ticket resolution", "Customer feedback very positive" };
        var negatives = new[] { "Needs to improve call handling time", "Occasional late logins", "Documentation could be more detailed", "Follow-up emails sometimes delayed" };

        foreach (var agent in agents)
        {
            var teamIdx = agents.IndexOf(agent) / 5;
            var leader  = leaders[teamIdx];

            for (int r = 0; r < 2; r++)
            {
                var reviewDate  = today.AddDays(r == 0 ? -42 : -14);
                var periodLabel = reviewDate.ToString("MMMM yyyy");

                var review = new PerformanceReview
                {
                    AgentId            = agent.Id,
                    ReviewedByLeaderId = leader.Id,
                    ReviewDate         = reviewDate,
                    PeriodLabel        = periodLabel,
                    OverallNotes       = $"Review for {agent.DisplayName} — {periodLabel}. Performance is satisfactory.",
                    CreatedAt          = reviewDate
                };

                review.Items.Add(new ReviewItem { Category = ReviewCategory.Ticket, ReferenceId = $"TKT-{rng.Next(1000,9999)}", Rating = rng.Next(6,11), Positive = positives[rng.Next(positives.Length)], Negative = negatives[rng.Next(negatives.Length)], ActionRequired = rng.Next(5) == 0 });
                review.Items.Add(new ReviewItem { Category = ReviewCategory.Chat,   ReferenceId = $"CHT-{rng.Next(1000,9999)}", Rating = rng.Next(6,11), Positive = positives[rng.Next(positives.Length)], Negative = negatives[rng.Next(negatives.Length)], ActionRequired = rng.Next(5) == 0 });
                review.Items.Add(new ReviewItem { Category = ReviewCategory.Call,   ReferenceId = $"CALL-{rng.Next(1000,9999)}",Rating = rng.Next(6,11), Positive = positives[rng.Next(positives.Length)], Negative = negatives[rng.Next(negatives.Length)], ActionRequired = rng.Next(5) == 0, ActionNote = rng.Next(5) == 0 ? "Follow up required next review" : null });

                db.PerformanceReviews.Add(review);
            }
        }
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Performance reviews saved.");

        // ════════════════════════════════════════════════════════════════
        // 12. POI SIMULATIONS (5–8 per brand)
        // ════════════════════════════════════════════════════════════════
        for (int b = 0; b < brands.Length; b++)
        {
            var brand      = brands[b];
            var brandAgents = agents.Skip(b * 5).Take(5).ToList();
            int count       = rng.Next(5, 9);

            for (int i = 0; i < count; i++)
            {
                var simDate   = today.AddDays(-rng.Next(1, 30));
                var loggedBy  = brandAgents[rng.Next(brandAgents.Count)];
                bool received = rng.Next(10) < 6; // 60% received

                db.PoiSimulations.Add(new PoiSimulation
                {
                    ClientId     = $"CLT-{rng.Next(10000, 99999)}",
                    BrandId      = brand.Id,
                    SimulatedAt  = simDate,
                    LoggedById   = loggedBy.Id,
                    Notes        = "Routine POI simulation for compliance check.",
                    PoiReceived  = received,
                    ReceivedAt   = received ? simDate.AddDays(rng.Next(1, 3)) : null,
                    ReceivedById = received ? loggedBy.Id : null
                });
            }
        }
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] POI simulations saved.");

        // ════════════════════════════════════════════════════════════════
        // 13. UPDATES / ANNOUNCEMENTS (10)
        // ════════════════════════════════════════════════════════════════
        var updateDefs = new[]
        {
            (manager1, "System Maintenance Notice",          "<p>Scheduled maintenance this Saturday 02:00–04:00 UTC. All platforms will be briefly offline.</p>",                        true,  new[] { "System"     }, new[] { 0, 1 }),
            (manager1, "New Compliance Policy — AML Update", "<p>Please review the updated AML policy document attached. All agents must acknowledge by end of week.</p>",               true,  new[] { "Compliance" }, new[] { 0, 1 }),
            (manager2, "Holiday Trading Hours",              "<p>Reduced trading hours apply over the upcoming public holiday. See schedule for details.</p>",                            true,  new[] { "Holiday"    }, new[] { 2, 3, 4 }),
            (manager1, "CRM v4.2 Released",                 "<p>The CRM has been upgraded to version 4.2. Key changes: faster search, new client tagging, bulk email improvements.</p>",false, new[] { "System"     }, new[] { 0, 1 }),
            (manager2, "POI Process Update",                "<p>Effective immediately, all POI requests must be logged within 24 hours of client contact.</p>",                          false, new[] { "Process"    }, new[] { 2, 3, 4 }),
            (manager1, "Team Performance — May Results",    "<p>Great work everyone! May targets were exceeded across all teams. Full breakdown available in Reports.</p>",              false, new[] { "Policy"     }, new[] { 0, 1, 2 }),
            (manager2, "Vault Password Reset Reminder",     "<p>Reminder to rotate vault credentials every 90 days. Please update any credentials older than 90 days.</p>",             false, new[] { "Policy"     }, new[] { 3, 4 }),
            (manager1, "New Brand Onboarding — SilkRoute",  "<p>Welcome to SilkRoute Invest! All agents assigned to this brand have been briefed. Resources are in the Vault.</p>",    false, new[] { "Process"    }, new[] { 4 }),
            (manager2, "Weekend Shift Volunteers Needed",   "<p>We have open weekend slots for next month. Please contact your team leader if you are available.</p>",                  false, new[] { "Policy"     }, new[] { 2, 3 }),
            (manager1, "Redmine Ticketing Reminder",        "<p>All client queries must be logged in Redmine within 2 hours. Failure to log results in SLA breach.</p>",               false, new[] { "Process"    }, new[] { 0, 1, 2, 3, 4 })
        };

        foreach (var (author, title, body, pinned, tags, brandIdxArr) in updateDefs)
        {
            var update = new Update
            {
                Title     = title,
                Body      = body,
                AuthorId  = author.Id,
                IsPinned  = pinned,
                IsArchived = false,
                CreatedAt = today.AddDays(-rng.Next(1, 20)),
                UpdatedAt = DateTime.UtcNow
            };
            update.SetTags(tags);
            foreach (var bi in brandIdxArr)
                update.AffectedBrands.Add(new UpdateBrand { BrandId = brands[bi].Id });

            db.Updates.Add(update);
        }
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Updates saved.");

        // ════════════════════════════════════════════════════════════════
        // 14. VAULT — extra categories + entries (2 per agent)
        // ════════════════════════════════════════════════════════════════
        // Default categories (Id 1,2,3) already exist from HasData seed.
        // Add a 4th: "Admin Portals"
        var vaultCatAdmin = new VaultCategory { Name = "Admin Portals", IconCssClass = "bx bx-shield", IsCustom = false };
        db.VaultCategories.Add(vaultCatAdmin);
        await db.SaveChangesAsync();

        // Reload existing categories to get stable IDs
        var catCrm      = await db.VaultCategories.FirstAsync(c => c.Name == "CRM");
        var catEmail    = await db.VaultCategories.FirstAsync(c => c.Name == "Quemetrics");

        var vaultEntries = new List<VaultEntry>();
        foreach (var agent in agents)
        {
            var teamIdx = agents.IndexOf(agent) / 5;
            var leader  = leaders[teamIdx];
            var brand   = brands[teamIdx];

            vaultEntries.Add(new VaultEntry
            {
                OwnerId             = agent.Id,
                CategoryId          = catCrm.Id,
                Label               = $"{brand.Name} CRM",
                Username            = agent.Email!,
                EncryptedPassword   = protector.Protect($"CRM-Pass-{rng.Next(1000,9999)}!"),
                Url                 = brand.CrmUrl,
                Notes               = "Auto-provisioned on onboarding.",
                CreatedAt           = DateTime.UtcNow,
                UpdatedAt           = DateTime.UtcNow,
                ProvisionedByUserId = leader.Id
            });

            vaultEntries.Add(new VaultEntry
            {
                OwnerId             = agent.Id,
                CategoryId          = catEmail.Id,
                Label               = $"{brand.Name} Quemetrics",
                Username            = agent.Email!,
                EncryptedPassword   = protector.Protect($"Qmet-Pass-{rng.Next(1000,9999)}!"),
                Url                 = brand.QuemetricsUrl,
                Notes               = "Quemetrics call-tracking account.",
                CreatedAt           = DateTime.UtcNow,
                UpdatedAt           = DateTime.UtcNow,
                ProvisionedByUserId = leader.Id
            });
        }
        db.VaultEntries.AddRange(vaultEntries);
        await db.SaveChangesAsync();
        Console.WriteLine($"[DemoSeed] {vaultEntries.Count} vault entries saved.");

        // ════════════════════════════════════════════════════════════════
        // 15. WORK DISTRIBUTION (Mon–Fri of current week, one per team)
        // ════════════════════════════════════════════════════════════════
        var weekMon = today.AddDays(-(int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1);
        var wdEntries = new List<WorkDistribution>();

        for (int t = 0; t < teams.Count; t++)
        {
            var leader     = leaders[t];
            var teamAgents = agents.Skip(t * 5).Take(5).ToList();

            for (int d = 0; d < 5; d++)
            {
                var date = weekMon.AddDays(d);
                if (date > today) break;

                var body = $"📋 Work Distribution — {date:dddd dd MMM yyyy}\n\n" +
                           string.Join("\n", teamAgents.Select((a, i) =>
                               $"@{a.DisplayName} — {(i % 3 == 0 ? "Chat queue + Tickets" : i % 3 == 1 ? "Phone queue + Tickets" : "Tickets only")}"));

                wdEntries.Add(new WorkDistribution
                {
                    Date        = date,
                    Body        = body,
                    CreatedById = leader.Id,
                    CreatedAt   = date.AddHours(8),
                    UpdatedAt   = date.AddHours(8)
                });
            }
        }
        db.WorkDistributions.AddRange(wdEntries);
        await db.SaveChangesAsync();
        Console.WriteLine($"[DemoSeed] {wdEntries.Count} work distribution entries saved.");

        // ════════════════════════════════════════════════════════════════
        // 16. CS LIVE HELP SLOTS (current week, agents with HasCsLiveHelp)
        // ════════════════════════════════════════════════════════════════
        var csSlots   = new List<CsLiveHelpSlot>();
        var csAgents  = agents.Where(a => a.HasCsLiveHelp).ToList();

        if (csAgents.Count >= 2)
        {
            for (int d = 0; d < 5; d++)
            {
                var slotDate = weekMon.AddDays(d);
                if (slotDate > today) break;

                // Who created this day's schedule — find the team leader for the first available cs agent
                var creatorLeader = leaders[0];

                int agentCursor = 0;
                for (int hour = 8; hour <= 20; hour++)
                {
                    var a1 = csAgents[agentCursor % csAgents.Count];
                    var a2 = csAgents[(agentCursor + 1) % csAgents.Count];
                    agentCursor += 2;

                    csSlots.Add(new CsLiveHelpSlot
                    {
                        Date        = slotDate,
                        SlotHour    = hour,
                        Agent1Id    = a1.Id,
                        Agent2Id    = a2.Id,
                        CreatedById = creatorLeader.Id,
                        CreatedAt   = slotDate.AddHours(7),
                        UpdatedAt   = slotDate.AddHours(7)
                    });
                }
            }
        }
        db.CsLiveHelpSlots.AddRange(csSlots);
        await db.SaveChangesAsync();
        Console.WriteLine($"[DemoSeed] {csSlots.Count} CS Live Help slots saved.");

        Console.WriteLine("[DemoSeed] ✅ Demo data load complete.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task<AppUser> CreateUserAsync(
        UserManager<AppUser> userManager, AppUser user, string password, string role)
    {
        // Skip if already exists
        var existing = await userManager.FindByEmailAsync(user.Email!);
        if (existing != null) return existing;

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to create user {user.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static async Task AddTeamReport(
        AppDbContext db, Random rng, Team team, AppUser leader,
        List<AppUser> teamAgents, PeriodType periodType, DateTime start, DateTime end)
    {
        var stats = teamAgents.Select(a => new AgentStat
        {
            AgentId  = a.Id,
            Chats    = rng.Next(40, 121),
            Tickets  = rng.Next(10, 51),
            Calls    = rng.Next(5,  31),
            FTD      = rng.Next(0,  6),
            Language = a.Language
        }).ToList();

        // Mark top performers
        stats[stats.Select((s, i) => (s.Chats,   i)).MaxBy(x => x.Chats).i].IsTopChatPicker    = true;
        stats[stats.Select((s, i) => (s.Tickets, i)).MaxBy(x => x.Tickets).i].IsTopTicketSolver = true;
        stats[stats.Select((s, i) => (s.Calls,   i)).MaxBy(x => x.Calls).i].IsTopCallMaker      = true;

        // FTD language breakdown
        var ftdByLang = stats
            .GroupBy(s => s.Language ?? "EN")
            .Select(g => new FTDLanguageStat { Language = g.Key, FTDCount = g.Sum(s => s.FTD) })
            .ToList();

        var report = new TeamReport
        {
            TeamId             = team.Id,
            ReportedByLeaderId = leader.Id,
            PeriodType         = periodType,
            PeriodStart        = start,
            PeriodEnd          = end,
            TotalChats         = stats.Sum(s => s.Chats),
            TotalTickets       = stats.Sum(s => s.Tickets),
            TotalCalls         = stats.Sum(s => s.Calls),
            TotalFTD           = stats.Sum(s => s.FTD),
            SubmittedAt        = end.AddDays(1),
            AgentStats         = stats,
            FTDLanguageStats   = ftdByLang
        };

        db.TeamReports.Add(report);
        await db.SaveChangesAsync();
    }
}
