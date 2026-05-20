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
using Unified.Models.ProcessTemplates;
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
        // 2. EMAIL TEMPLATES (multiple per brand)
        // ════════════════════════════════════════════════════════════════
        var emailTemplateDefs = new[]
        {
            // (brandIdx, title, subject, bodyHtml)
            (0, "NovaTrade FX \u2014 Client Welcome",
             "Welcome to NovaTrade FX \u2014 Your Account Is Ready",
             @"<html><body style='font-family:Arial,sans-serif;color:#333;'>" +
             @"<p>Dear {{ClientName}},</p>" +
             @"<p>Welcome to <strong style='color:#1A73E8;'>NovaTrade FX</strong> &mdash; a regulated forex broker trusted by traders worldwide.</p>" +
             @"<p>Your trading account has been successfully activated. Here is a summary of your next steps:</p>" +
             @"<ol><li>Log in to your account at <a href='https://novatradefx.demo'>novatradefx.demo</a></li>" +
             @"<li>Complete your KYC verification to unlock full account features</li>" +
             @"<li>Fund your account using one of our supported payment methods</li>" +
             @"<li>Start trading with access to 50+ currency pairs</li></ol>" +
             @"<p>If you have any questions, our support team is available 24/5 at <a href='mailto:dealing@novatradefx.demo'>dealing@novatradefx.demo</a>.</p>" +
             @"<hr/><p style='font-size:12px;'>NovaTrade FX | <a href='https://novatradefx.demo'>novatradefx.demo</a> | Regulated Forex Broker</p>" +
             @"</body></html>"),

            (0, "NovaTrade FX \u2014 KYC Reminder",
             "Action Required: Complete Your KYC Verification",
             @"<html><body style='font-family:Arial,sans-serif;color:#333;'>" +
             @"<p>Dear {{ClientName}},</p>" +
             @"<p>We noticed your KYC verification is still pending. To comply with regulatory requirements and unlock your full account, please submit the following documents:</p>" +
             @"<ul><li>Government-issued photo ID (passport or national ID)</li>" +
             @"<li>Proof of address dated within the last 3 months</li></ul>" +
             @"<p>Upload your documents securely at <a href='https://novatradefx.demo/kyc'>novatradefx.demo/kyc</a>.</p>" +
             @"<p>Your account will remain in restricted mode until verification is complete.</p>" +
             @"<hr/><p style='font-size:12px;'>NovaTrade FX | <a href='https://novatradefx.demo'>novatradefx.demo</a></p>" +
             @"</body></html>"),

            (1, "ApexMarkets \u2014 Client Welcome",
             "Bienvenue chez ApexMarkets \u2014 Votre Compte Est Pr\u00eat",
             @"<html><body style='font-family:Arial,sans-serif;color:#333;'>" +
             @"<p>Dear {{ClientName}},</p>" +
             @"<p>Welcome to <strong style='color:#E84C4C;'>ApexMarkets</strong> &mdash; your premier trading platform for global markets.</p>" +
             @"<p>Your account is now live. Here is what you can do next:</p>" +
             @"<ol><li>Access your dashboard at <a href='https://apexmarkets.demo'>apexmarkets.demo</a></li>" +
             @"<li>Review our Trading Conditions &amp; T&amp;C in your preferred language</li>" +
             @"<li>Set up two-factor authentication for account security</li>" +
             @"<li>Explore our market analysis tools and live economic calendar</li></ol>" +
             @"<p>Our dedicated support team speaks English and French. Reach us at <a href='mailto:dealing@apexmarkets.demo'>dealing@apexmarkets.demo</a>.</p>" +
             @"<hr/><p style='font-size:12px;'>ApexMarkets | <a href='https://apexmarkets.demo'>apexmarkets.demo</a> | Premier Trading Platform</p>" +
             @"</body></html>"),

            (1, "ApexMarkets \u2014 First Deposit Bonus",
             "Exclusive Offer: Claim Your First Deposit Bonus",
             @"<html><body style='font-family:Arial,sans-serif;color:#333;'>" +
             @"<p>Dear {{ClientName}},</p>" +
             @"<p>As a new ApexMarkets client you are eligible for our <strong>First Deposit Bonus</strong>. Here is how it works:</p>" +
             @"<ul><li>Deposit a minimum of &euro;250 to activate your bonus</li>" +
             @"<li>Bonus is credited to your trading account within 24 hours</li>" +
             @"<li>Bonus funds are tradeable across all major pairs</li></ul>" +
             @"<p>Terms and conditions apply. See our full bonus policy at <a href='https://docs.apexmarkets.demo/tc-fr'>apexmarkets.demo/tc</a>.</p>" +
             @"<p>To make your first deposit, log in at <a href='https://apexmarkets.demo'>apexmarkets.demo</a>.</p>" +
             @"<hr/><p style='font-size:12px;'>ApexMarkets | <a href='https://apexmarkets.demo'>apexmarkets.demo</a></p>" +
             @"</body></html>"),

            (2, "ZenithCapital \u2014 Client Welcome",
             "Welcome to ZenithCapital \u2014 Wealth at Your Fingertips",
             @"<html><body style='font-family:Arial,sans-serif;color:#333;'>" +
             @"<p>Estimado {{ClientName}},</p>" +
             @"<p>Welcome to <strong style='color:#2ECC71;'>ZenithCapital</strong> &mdash; where intelligent investing meets modern technology.</p>" +
             @"<p>Your investment account is now open and ready. To get started:</p>" +
             @"<ol><li>Log in at <a href='https://zenithcapital.demo'>zenithcapital.demo</a></li>" +
             @"<li>Review your personalised risk profile</li>" +
             @"<li>Explore our curated portfolio strategies</li>" +
             @"<li>Fund your account via bank transfer or card payment</li></ol>" +
             @"<p>Our bilingual (English &amp; Spanish) support team is available Mon&ndash;Fri 08:00&ndash;20:00 CET at <a href='mailto:dealing@zenithcapital.demo'>dealing@zenithcapital.demo</a>.</p>" +
             @"<hr/><p style='font-size:12px;'>ZenithCapital | <a href='https://zenithcapital.demo'>zenithcapital.demo</a> | Wealth Management</p>" +
             @"</body></html>"),

            (2, "ZenithCapital \u2014 Monthly Portfolio Report",
             "Your Monthly Portfolio Summary \u2014 {{PeriodLabel}}",
             @"<html><body style='font-family:Arial,sans-serif;color:#333;'>" +
             @"<p>Dear {{ClientName}},</p>" +
             @"<p>Please find below your portfolio performance summary for <strong>{{PeriodLabel}}</strong>:</p>" +
             @"<table style='width:100%;border-collapse:collapse;'>" +
             @"<tr style='background:#2ECC71;color:#fff;'><th style='padding:8px;'>Asset</th><th style='padding:8px;'>Allocation</th><th style='padding:8px;'>Return</th></tr>" +
             @"<tr><td style='padding:6px;'>Equities</td><td style='padding:6px;'>{{EquityAlloc}}%</td><td style='padding:6px;'>{{EquityReturn}}%</td></tr>" +
             @"<tr style='background:#f5f5f5;'><td style='padding:6px;'>Fixed Income</td><td style='padding:6px;'>{{BondAlloc}}%</td><td style='padding:6px;'>{{BondReturn}}%</td></tr>" +
             @"<tr><td style='padding:6px;'>Commodities</td><td style='padding:6px;'>{{CommodityAlloc}}%</td><td style='padding:6px;'>{{CommodityReturn}}%</td></tr>" +
             @"</table>" +
             @"<p>For a full breakdown, log in at <a href='https://zenithcapital.demo'>zenithcapital.demo</a>.</p>" +
             @"<hr/><p style='font-size:12px;'>ZenithCapital | <a href='https://zenithcapital.demo'>zenithcapital.demo</a></p>" +
             @"</body></html>"),

            (3, "PrimeVault \u2014 Client Welcome",
             "Bem-vindo \u00e0 PrimeVault \u2014 Seguro e Lucrativo",
             @"<html><body style='font-family:Arial,sans-serif;color:#333;'>" +
             @"<p>Dear {{ClientName}},</p>" +
             @"<p>Welcome to <strong style='color:#9B59B6;'>PrimeVault</strong> &mdash; your secure gateway to high-performance investment products.</p>" +
             @"<p>Your account has been created. Here are your next steps:</p>" +
             @"<ol><li>Sign in at <a href='https://primevault.demo'>primevault.demo</a></li>" +
             @"<li>Set your investment goals and risk tolerance in the client profile section</li>" +
             @"<li>Upload your KYC documentation to unlock premium products</li>" +
             @"<li>Contact your dedicated account manager for a personalised onboarding call</li></ol>" +
             @"<p>Questions? Contact us at <a href='mailto:dealing@primevault.demo'>dealing@primevault.demo</a>. We speak English and Portuguese.</p>" +
             @"<hr/><p style='font-size:12px;'>PrimeVault | <a href='https://primevault.demo'>primevault.demo</a> | Secure &amp; Profitable</p>" +
             @"</body></html>"),

            (3, "PrimeVault \u2014 Account Upgrade Offer",
             "Upgrade to PrimeVault Premium \u2014 Exclusive Benefits Await",
             @"<html><body style='font-family:Arial,sans-serif;color:#333;'>" +
             @"<p>Dear {{ClientName}},</p>" +
             @"<p>Based on your trading activity, you qualify for a <strong>PrimeVault Premium</strong> account upgrade. Benefits include:</p>" +
             @"<ul><li>Dedicated account manager available 7 days a week</li>" +
             @"<li>Tighter spreads on all major instruments</li>" +
             @"<li>Access to exclusive structured products</li>" +
             @"<li>Priority withdrawal processing (same-day)</li></ul>" +
             @"<p>To accept this offer or to learn more, reply to this email or call your account manager directly.</p>" +
             @"<p>Offer valid for 14 days from receipt of this email.</p>" +
             @"<hr/><p style='font-size:12px;'>PrimeVault | <a href='https://primevault.demo'>primevault.demo</a></p>" +
             @"</body></html>"),

            (4, "SilkRoute Invest \u2014 Client Welcome",
             "Welcome to SilkRoute Invest \u2014 Connecting Eastern Markets",
             @"<html><body style='font-family:Arial,sans-serif;color:#333;'>" +
             @"<p>Dear {{ClientName}},</p>" +
             @"<p>Welcome to <strong style='color:#F39C12;'>SilkRoute Invest</strong> &mdash; bridging investors to the world's most dynamic emerging markets.</p>" +
             @"<p>Your account is now active. Get started in three simple steps:</p>" +
             @"<ol><li>Log in at <a href='https://silkrouteinvest.demo'>silkrouteinvest.demo</a></li>" +
             @"<li>Complete your investor profile and select your preferred markets</li>" +
             @"<li>Fund your account &mdash; we accept multiple currencies including USD, EUR, and AED</li></ol>" +
             @"<p>Our multilingual team (English &amp; Arabic) is ready to assist you at <a href='mailto:dealing@silkrouteinvest.demo'>dealing@silkrouteinvest.demo</a>.</p>" +
             @"<hr/><p style='font-size:12px;'>SilkRoute Invest | <a href='https://silkrouteinvest.demo'>silkrouteinvest.demo</a> | Connecting Eastern Markets</p>" +
             @"</body></html>"),

            (4, "SilkRoute Invest \u2014 Ramadan Trading Hours",
             "Important: Adjusted Trading Hours During Ramadan",
             @"<html><body style='font-family:Arial,sans-serif;color:#333;'>" +
             @"<p>Dear {{ClientName}},</p>" +
             @"<p>During the holy month of <strong>Ramadan</strong>, SilkRoute Invest will be adjusting our customer support hours to better serve our valued clients:</p>" +
             @"<ul><li><strong>Sunday &ndash; Thursday:</strong> 10:00 &ndash; 18:00 GST</li>" +
             @"<li><strong>Friday &ndash; Saturday:</strong> Closed</li></ul>" +
             @"<p>Trading platforms remain available 24/5 as usual. Automated services are unaffected.</p>" +
             @"<p>We wish all our clients observing Ramadan a blessed and peaceful month. Ramadan Kareem!</p>" +
             @"<p>For urgent queries outside support hours, email <a href='mailto:dealing@silkrouteinvest.demo'>dealing@silkrouteinvest.demo</a>.</p>" +
             @"<hr/><p style='font-size:12px;'>SilkRoute Invest | <a href='https://silkrouteinvest.demo'>silkrouteinvest.demo</a></p>" +
             @"</body></html>"),
        };

        foreach (var (brandIdx, title, subject, body) in emailTemplateDefs)
        {
            db.EmailTemplates.Add(new EmailTemplate
            {
                Title       = title,
                SubjectLine = subject,
                BodyHtml    = body,
                BrandId     = brands[brandIdx].Id,
                IsActive    = true,
                CreatedAt   = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Email templates saved.");

        // ════════════════════════════════════════════════════════════════
        // 3. PROCESS TEMPLATE CATEGORIES + TEMPLATES
        // ════════════════════════════════════════════════════════════════
        var ptCatCompliance  = new TemplateCategory { Name = "Compliance",       IconCssClass = "bx bx-shield-quarter", SortOrder = 1 };
        var ptCatOnboarding  = new TemplateCategory { Name = "Onboarding",        IconCssClass = "bx bx-user-plus",      SortOrder = 2 };
        var ptCatKyc         = new TemplateCategory { Name = "KYC / AML",         IconCssClass = "bx bx-id-card",        SortOrder = 3 };
        var ptCatSupport     = new TemplateCategory { Name = "Client Support",    IconCssClass = "bx bx-headphone",      SortOrder = 4 };
        var ptCatInternal    = new TemplateCategory { Name = "Internal Process",  IconCssClass = "bx bx-cog",            SortOrder = 5 };
        db.TemplateCategories.AddRange(ptCatCompliance, ptCatOnboarding, ptCatKyc, ptCatSupport, ptCatInternal);
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Process template categories saved.");

        // Process templates — global (BrandId null) unless brand-specific
        var processTemplateDefs = new[]
        {
            // ── Compliance ────────────────────────────────────────────────
            (ptCatCompliance, "AML Suspicious Activity Report",
             "Use this template when logging a Suspicious Activity Report (SAR) following identification of unusual client behaviour.",
             @"1. IDENTIFY the suspicious behaviour and document all relevant transaction IDs, dates, and amounts.
2. DO NOT inform the client that a SAR is being filed — this constitutes 'tipping off' and is a regulatory offence.
3. Complete the internal SAR form in the CRM under Client > Compliance > SAR.
4. Notify the AML Officer via the Redmine AML queue within 2 business hours.
5. Retain all supporting documentation in the client's compliance folder.
6. Await instruction from the AML Officer before taking any further action on the account.

REMINDER: All SARs are strictly confidential. Discuss only with authorised personnel.",
             "Ensure the AML Officer acknowledgement is received and logged in Redmine before closing this task.",
             new[] { 0, 1, 2, 3, 4 }),

            (ptCatCompliance, "PEP / Sanctions Screening Procedure",
             "Follow this process whenever a client or prospective client appears on a PEP or sanctions watchlist.",
             @"1. Run the client's full name, date of birth, and nationality through the sanctions screening tool in the CRM.
2. If a match is returned, immediately freeze all pending transactions on the account.
3. Escalate to the AML Officer with the screening report attached.
4. Do not process any deposits, withdrawals, or account changes until clearance is given.
5. Document the screening result and escalation in Redmine under the client's compliance ticket.
6. If the client is cleared, re-open the account and notify the client of a brief maintenance hold (do not disclose the real reason).
7. If the client is confirmed as a PEP or sanctioned entity, follow the account closure procedure.",
             "Screening must be repeated at each deposit exceeding the defined threshold.",
             new[] { 0, 1, 2, 3, 4 }),

            // ── Onboarding ────────────────────────────────────────────────
            (ptCatOnboarding, "New Client Onboarding Checklist",
             "Standard checklist to complete when a new live account is approved and ready for first contact.",
             @"1. Send the Welcome Email template from the Email Templates module.
2. Log the first contact attempt in the CRM under the client's activity timeline.
3. Confirm the client has received their login credentials and can access the platform.
4. Walk the client through the funding process and available payment methods.
5. Set a follow-up reminder in 48 hours if the client has not yet made a first deposit.
6. Once the first deposit is confirmed, tag the client as 'Active' in the CRM.
7. Assign the client to the appropriate team bucket based on deposit size and language.
8. Notify the team leader of any first-time deposit (FTD) via the Reports module.",
             "If the client cannot be reached after 3 attempts, escalate to the team leader.",
             new[] { 0, 1, 2, 3, 4 }),

            (ptCatOnboarding, "Demo Account to Live Account Conversion",
             "Steps to guide a demo account user through the conversion to a funded live account.",
             @"1. Review the client's demo trading history in the CRM to assess readiness.
2. Contact the client via their preferred channel to discuss the transition.
3. Highlight the key differences between demo and live trading (spreads, slippage, psychology).
4. Send the account upgrade link and guide the client through the live application form.
5. Once the application is submitted, monitor the approval queue and notify the client when approved.
6. Send the Welcome Email and complete the New Client Onboarding Checklist.
7. Offer an introductory call with a senior account manager if the client has questions about live trading strategies.",
             "Conversion rate from demo to live is a tracked KPI — log all successful conversions in the Reports module.",
             new[] { 0, 1, 2, 3, 4 }),

            // ── KYC / AML ─────────────────────────────────────────────────
            (ptCatKyc, "KYC Document Collection & Verification",
             "Process for collecting, reviewing, and approving client identity and address verification documents.",
             @"1. Send the KYC request email to the client with the list of required documents.
2. Required documents: (a) Government-issued photo ID — passport or national ID card. (b) Proof of address — utility bill, bank statement, or official letter dated within 3 months.
3. Once documents are received, review for: clarity, validity, name match, address match, expiry date.
4. If documents pass: update the client's CRM profile to KYC Approved and log the verification date.
5. If documents fail: send the KYC Rejection email specifying what needs to be re-submitted.
6. If a third document is submitted and still fails, escalate to the AML Officer.
7. Log all KYC activity in Redmine under the client's KYC ticket.",
             "KYC must be completed before any withdrawal is processed, regardless of account balance.",
             new[] { 0, 1, 2, 3, 4 }),

            (ptCatKyc, "Source of Funds (SOF) Request",
             "Use when a client's cumulative deposits exceed the enhanced due diligence threshold or when unusual deposit patterns are observed.",
             @"1. Log the SOF trigger in Redmine under the client's compliance ticket.
2. Send the SOF Request email template explaining what is required.
3. Acceptable SOF documentation includes: employment contract or payslip, business ownership documents, inheritance or sale of assets documentation, investment or savings account statements.
4. Review submitted SOF documents for plausibility against the client's trading activity.
5. If approved: update the CRM enhanced due diligence flag and log approval with document references.
6. If declined: freeze withdrawals and escalate to the AML Officer within 24 hours.
7. SOF reviews must be repeated annually for high-volume clients.",
             "Never pressure a client to submit SOF documents under urgency — this can constitute mis-selling.",
             new[] { 0, 1, 2, 3, 4 }),

            // ── Client Support ────────────────────────────────────────────
            (ptCatSupport, "Withdrawal Request Handling",
             "Standard process for handling a client withdrawal request from initial submission to completion.",
             @"1. Receive the withdrawal request via the CRM or client portal.
2. Verify the client's KYC status is Approved. If not, pause and notify the client.
3. Verify the withdrawal method matches a previously used deposit method (AML same-source rule).
4. Check for any open bonus conditions that may prevent the withdrawal.
5. Approve the request in the back-office system and submit to the finance team.
6. Notify the client that the request is being processed (standard processing time 3–5 business days).
7. If the withdrawal is flagged or delayed by the bank, notify the client with an updated ETA.
8. Once confirmed, log the completed withdrawal in the CRM and close the Redmine ticket.",
             "Withdrawals must never be reversed without written authorisation from the compliance team.",
             new[] { 0, 1, 2, 3, 4 }),

            (ptCatSupport, "Client Complaint Resolution",
             "Follow this process for all formal client complaints to ensure timely and compliant resolution.",
             @"1. Acknowledge the complaint in writing within 24 hours of receipt.
2. Log the complaint in Redmine under the appropriate brand's Complaints queue.
3. Assign a unique complaint reference number and communicate it to the client.
4. Investigate: review all interaction history, trade logs, and correspondence.
5. If the complaint relates to a trade execution issue, involve the dealing desk.
6. Aim to resolve the complaint within 5 business days. If more time is needed, issue an interim response.
7. Draft the final response letter and have it reviewed by the team leader before sending.
8. Close the complaint in the CRM and Redmine, noting the resolution and any compensatory action taken.
9. If the client remains unsatisfied, provide information on escalation to the relevant regulatory body.",
             "All complaints must be logged regardless of whether the client sends a formal written complaint.",
             new[] { 0, 1, 2, 3, 4 }),

            (ptCatSupport, "Platform Technical Issue — Client Escalation",
             "Steps to follow when a client reports a technical issue with the trading platform or portal.",
             @"1. Gather full details: platform (Web/Desktop/Mobile), operating system, browser/version, error message, time of occurrence.
2. Attempt to reproduce the issue in the testing environment.
3. If reproducible: log a bug report in Redmine under the relevant brand's Tech queue with all collected details.
4. If not reproducible: ask the client for a screenshot or screen recording.
5. Provide the client with a Redmine ticket reference and estimated investigation timeline.
6. If the issue is causing live trading disruption, escalate to the senior support team immediately.
7. Once resolved, contact the client, explain what was fixed, and close the ticket.",
             null,
             new[] { 0, 1, 2, 3, 4 }),

            // ── Internal Process ──────────────────────────────────────────
            (ptCatInternal, "End-of-Day Shift Handover",
             "Checklist to complete before handing over to the next shift to ensure continuity of client service.",
             @"1. Review all open Redmine tickets assigned to you and update statuses.
2. For any ticket that cannot be resolved before end of shift, add a progress note and re-assign to the incoming shift lead.
3. Log any client accounts requiring urgent follow-up in the Handover Notes shared document.
4. Ensure all calls, chats, and emails for the day are logged in the CRM.
5. Update your attendance log if your check-out time differs from your scheduled shift end.
6. Brief the incoming team lead verbally or via the team messaging channel on any priority issues.
7. Complete your shift summary in the Reports module if required by your team leader.",
             "Incomplete handovers are one of the leading causes of SLA breaches. Do not skip this process.",
             new[] { 0, 1, 2, 3, 4 }),

            (ptCatInternal, "New Agent Onboarding — Internal Setup",
             "IT and admin checklist for team leaders when a new agent joins the team.",
             @"1. Create the agent's account in the Unified platform and assign the correct role (CSAgent).
2. Assign the agent to the correct team and brand in the system.
3. Provision CRM credentials via the Vault module and share the vault entry with the agent.
4. Provision Quemetrics call-tracking account and add to the Vault.
5. Ensure the agent has access to the Redmine project for their brand.
6. Schedule a 1-hour platform orientation session within the agent's first week.
7. Add the agent to the team's CS Live Help rota for the following week.
8. Send the agent the internal onboarding document pack via email.
9. Complete the agent's first performance review at the end of their 30-day probationary period.",
             "All provisioned credentials must be stored in the Vault — never shared via email or chat.",
             new[] { 0, 1, 2, 3, 4 }),
        };

        foreach (var (cat, title, desc, bodyText, guidance, brandIdxArr) in processTemplateDefs)
        {
            var pt = new ProcessTemplate
            {
                Title          = title,
                CategoryId     = cat.Id,
                Description    = desc,
                BodyText       = bodyText,
                GuidanceNotes  = guidance,
                IsActive       = true,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow
            };
            // Link to all applicable brands
            foreach (var bi in brandIdxArr)
                pt.AffectedBrands.Add(new ProcessTemplateBrand { BrandId = brands[bi].Id });
            db.ProcessTemplates.Add(pt);
        }
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Process templates saved.");

        // ════════════════════════════════════════════════════════════════
        // 4. MANAGERS (2)
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
        // 5. TEAM LEADERS (5)
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
        // 6. TEAMS (5) — created before agents so we have IDs
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
        // 7. CS AGENTS (25)
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
        // 8. SHIFT TEMPLATES
        // ════════════════════════════════════════════════════════════════
        var shiftMorning = new ShiftTemplate { Name = "Morning Shift",   StartTime = new TimeSpan(7,  0, 0), EndTime = new TimeSpan(15, 0, 0), IsWeekendShift = false };
        var shiftAfter   = new ShiftTemplate { Name = "Afternoon Shift", StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(22, 0, 0), IsWeekendShift = false };
        var shiftWeekend = new ShiftTemplate { Name = "Weekend Shift",   StartTime = new TimeSpan(9,  0, 0), EndTime = new TimeSpan(17, 0, 0), IsWeekendShift = true  };
        db.ShiftTemplates.AddRange(shiftMorning, shiftAfter, shiftWeekend);
        await db.SaveChangesAsync();
        Console.WriteLine("[DemoSeed] Shift templates saved.");

        // ════════════════════════════════════════════════════════════════
        // 9. AGENT SCHEDULES (4 weeks)
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
        // 10. ATTENDANCE LOGS (last 30 days)
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
        // 11. TEAM REPORTS (4 weekly + 1 monthly per team)
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
        // 12. PERFORMANCE REVIEWS (2 per agent)
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
        // 13. POI SIMULATIONS (5–8 per brand)
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
        // 14. UPDATES / ANNOUNCEMENTS (10)
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
        // 15. VAULT — extra categories + entries (4 per agent)
        // ════════════════════════════════════════════════════════════════
        // Add two additional runtime categories; Id 1–4 already exist from HasData.
        var vaultCatTrading = new VaultCategory { Name = "Trading Platform", IconCssClass = "bx bx-line-chart",  IsCustom = false };
        var vaultCatAdmin   = new VaultCategory { Name = "Admin Portals",    IconCssClass = "bx bx-shield",       IsCustom = false };
        db.VaultCategories.AddRange(vaultCatTrading, vaultCatAdmin);
        await db.SaveChangesAsync();

        // Reload existing categories by name to get stable IDs
        var catCrm      = await db.VaultCategories.FirstAsync(c => c.Name == "CRM");
        var catQuemetrics = await db.VaultCategories.FirstAsync(c => c.Name == "Quemetrics");
        var catRedmine  = await db.VaultCategories.FirstAsync(c => c.Name == "Redmine");
        var catTrading  = await db.VaultCategories.FirstAsync(c => c.Name == "Trading Platform");
        var catAdmin    = await db.VaultCategories.FirstAsync(c => c.Name == "Admin Portals");

        // Brand-specific trading platform and back-office admin URLs
        var tradingUrls = new[]
        {
            ("https://platform.novatradefx.demo",   "https://backoffice.novatradefx.demo"),
            ("https://platform.apexmarkets.demo",    "https://backoffice.apexmarkets.demo"),
            ("https://platform.zenithcapital.demo",  "https://backoffice.zenithcapital.demo"),
            ("https://platform.primevault.demo",     "https://backoffice.primevault.demo"),
            ("https://platform.silkrouteinvest.demo","https://backoffice.silkrouteinvest.demo"),
        };

        var vaultEntries = new List<VaultEntry>();
        foreach (var agent in agents)
        {
            var teamIdx  = agents.IndexOf(agent) / 5;
            var leader   = leaders[teamIdx];
            var brand    = brands[teamIdx];
            var (tradingUrl, adminUrl) = tradingUrls[teamIdx];

            // 1. CRM
            vaultEntries.Add(new VaultEntry
            {
                OwnerId             = agent.Id,
                CategoryId          = catCrm.Id,
                Label               = $"{brand.Name} CRM",
                Username            = agent.Email!,
                EncryptedPassword   = protector.Protect($"CRM-Pass-{rng.Next(1000,9999)}!"),
                Url                 = brand.CrmUrl,
                Notes               = "Auto-provisioned on onboarding. Rotate every 90 days.",
                CreatedAt           = DateTime.UtcNow,
                UpdatedAt           = DateTime.UtcNow,
                ProvisionedByUserId = leader.Id
            });

            // 2. Quemetrics call-tracking
            vaultEntries.Add(new VaultEntry
            {
                OwnerId             = agent.Id,
                CategoryId          = catQuemetrics.Id,
                Label               = $"{brand.Name} Quemetrics",
                Username            = agent.Email!,
                EncryptedPassword   = protector.Protect($"Qmet-Pass-{rng.Next(1000,9999)}!"),
                Url                 = brand.QuemetricsUrl,
                Notes               = "Quemetrics call-tracking account. Used for call logging and QA scoring.",
                CreatedAt           = DateTime.UtcNow,
                UpdatedAt           = DateTime.UtcNow,
                ProvisionedByUserId = leader.Id
            });

            // 3. Trading Platform
            vaultEntries.Add(new VaultEntry
            {
                OwnerId             = agent.Id,
                CategoryId          = catTrading.Id,
                Label               = $"{brand.Name} Trading Platform",
                Username            = $"agent_{agent.UserName!.Split('@')[0]}",
                EncryptedPassword   = protector.Protect($"Trade-Pass-{rng.Next(1000,9999)}!"),
                Url                 = tradingUrl,
                Notes               = "Read-only agent access for monitoring client trading activity. Do not place trades.",
                CreatedAt           = DateTime.UtcNow,
                UpdatedAt           = DateTime.UtcNow,
                ProvisionedByUserId = leader.Id
            });

            // 4. Admin / Back-Office Portal
            vaultEntries.Add(new VaultEntry
            {
                OwnerId             = agent.Id,
                CategoryId          = catAdmin.Id,
                Label               = $"{brand.Name} Back Office",
                Username            = $"bo_{agent.UserName!.Split('@')[0]}",
                EncryptedPassword   = protector.Protect($"BO-Pass-{rng.Next(1000,9999)}!"),
                Url                 = adminUrl,
                Notes               = "Back-office portal for processing deposits, withdrawals, and account modifications. Use with caution — all actions are audited.",
                CreatedAt           = DateTime.UtcNow,
                UpdatedAt           = DateTime.UtcNow,
                ProvisionedByUserId = leader.Id
            });
        }

        // Also provision CRM + Admin entries for team leaders
        foreach (var leader in leaders)
        {
            var teamIdx  = leaders.IndexOf(leader);
            var brand    = brands[teamIdx];
            var (tradingUrl, adminUrl) = tradingUrls[teamIdx];

            vaultEntries.Add(new VaultEntry
            {
                OwnerId             = leader.Id,
                CategoryId          = catCrm.Id,
                Label               = $"{brand.Name} CRM — Leader",
                Username            = leader.Email!,
                EncryptedPassword   = protector.Protect($"CRM-Lead-{rng.Next(1000,9999)}!"),
                Url                 = brand.CrmUrl,
                Notes               = "Full CRM access for team leader. Includes client assignment and escalation rights.",
                CreatedAt           = DateTime.UtcNow,
                UpdatedAt           = DateTime.UtcNow,
                ProvisionedByUserId = leader.Id
            });

            vaultEntries.Add(new VaultEntry
            {
                OwnerId             = leader.Id,
                CategoryId          = catAdmin.Id,
                Label               = $"{brand.Name} Back Office — Leader",
                Username            = $"bo_lead_{leader.UserName!.Split('@')[0]}",
                EncryptedPassword   = protector.Protect($"BO-Lead-{rng.Next(1000,9999)}!"),
                Url                 = adminUrl,
                Notes               = "Elevated back-office access. Includes approval rights for withdrawals and account closures.",
                CreatedAt           = DateTime.UtcNow,
                UpdatedAt           = DateTime.UtcNow,
                ProvisionedByUserId = leader.Id
            });
        }

        db.VaultEntries.AddRange(vaultEntries);
        await db.SaveChangesAsync();
        Console.WriteLine($"[DemoSeed] {vaultEntries.Count} vault entries saved.");

        // ════════════════════════════════════════════════════════════════
        // 16. WORK DISTRIBUTION
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
        // 17. CS LIVE HELP SLOTS
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
