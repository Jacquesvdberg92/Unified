using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Unified.Models.Attendance;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;using Unified.Models.Performance;
using Unified.Models.ProcessTemplates;
using Unified.Models.Reports;
using Unified.Models.Schedule;
using Unified.Models.Updates;
using Unified.Models.Vault;
using Unified.Models.Poi;
using Unified.Models.WorkDistribution;
using Unified.Models.Dashboard;
using Unified.Models.CsLiveHelp;
using Unified.Models.Logging;

namespace Unified.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<AgentTeam> AgentTeams => Set<AgentTeam>();
    public DbSet<AgentBrand> AgentBrands => Set<AgentBrand>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<TemplateCategory> TemplateCategories => Set<TemplateCategory>();
    public DbSet<ProcessTemplate> ProcessTemplates => Set<ProcessTemplate>();
    public DbSet<ProcessTemplateBrand> ProcessTemplateBrands => Set<ProcessTemplateBrand>();
    public DbSet<Update> Updates => Set<Update>();
    public DbSet<UpdateBrand> UpdateBrands => Set<UpdateBrand>();
    public DbSet<ShiftTemplate> ShiftTemplates => Set<ShiftTemplate>();
    public DbSet<AgentSchedule> AgentSchedules => Set<AgentSchedule>();
    public DbSet<WeekendShiftOffer> WeekendShiftOffers => Set<WeekendShiftOffer>();
    public DbSet<TimeOffRequest> TimeOffRequests => Set<TimeOffRequest>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<ReviewItem> ReviewItems => Set<ReviewItem>();
    public DbSet<VaultCategory>    VaultCategories  => Set<VaultCategory>();
    public DbSet<VaultEntry>       VaultEntries     => Set<VaultEntry>();
    public DbSet<VaultAccessLog>   VaultAccessLogs  => Set<VaultAccessLog>();
    public DbSet<TeamReport>       TeamReports      => Set<TeamReport>();
    public DbSet<AgentStat>        AgentStats       => Set<AgentStat>();
    public DbSet<FTDLanguageStat>  FTDLanguageStats => Set<FTDLanguageStat>();

    // Attendance
    public DbSet<AttendanceLog>  AttendanceLogs   => Set<AttendanceLog>();
    public DbSet<PublicHoliday>  PublicHolidays   => Set<PublicHoliday>();

    // Work Distribution
    public DbSet<WorkDistribution>         WorkDistributions        => Set<WorkDistribution>();
    public DbSet<CsLiveAllocationSlot>     CsLiveAllocationSlots    => Set<CsLiveAllocationSlot>();
    public DbSet<CsLiveAllocationSwapLog>  CsLiveAllocationSwapLogs => Set<CsLiveAllocationSwapLog>();

    // POI Simulations
    public DbSet<PoiSimulation>       PoiSimulations       => Set<PoiSimulation>();

    // Dashboard
    public DbSet<DashboardWidget>     DashboardWidgets     => Set<DashboardWidget>();

    // Account requests
    public DbSet<AccountRequest>      AccountRequests      => Set<AccountRequest>();

    // Brand documents
    public DbSet<BrandDocument>       BrandDocuments       => Set<BrandDocument>();

    // CS Live Help – Kanban
    public DbSet<CsRequestType>       CsRequestTypes       => Set<CsRequestType>();
    public DbSet<CsRequest>           CsRequests           => Set<CsRequest>();
    public DbSet<CsRequestComment>    CsRequestComments    => Set<CsRequestComment>();
    public DbSet<CsRequestArchive>    CsRequestArchives    => Set<CsRequestArchive>();
    public DbSet<AmAuditLog>          AmAuditLogs          => Set<AmAuditLog>();

    // Activity + Error logging
    public DbSet<ActivityLog>         ActivityLogs         => Set<ActivityLog>();
    public DbSet<ErrorLog>            ErrorLogs            => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AgentTeam>().HasKey(at => new { at.AgentId, at.TeamId });
        builder.Entity<AgentTeam>()
            .HasOne(at => at.Agent)
            .WithMany(u => u.Teams)
            .HasForeignKey(at => at.AgentId);
        builder.Entity<AgentTeam>()
            .HasOne(at => at.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(at => at.TeamId);

        builder.Entity<BrandDocument>()
            .HasOne(d => d.Brand)
            .WithMany()
            .HasForeignKey(d => d.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AgentBrand>().HasKey(ab => new { ab.AgentId, ab.BrandId });
        builder.Entity<AgentBrand>()
            .HasOne(ab => ab.Agent)
            .WithMany(u => u.Brands)
            .HasForeignKey(ab => ab.AgentId);
        builder.Entity<AgentBrand>()
            .HasOne(ab => ab.Brand)
            .WithMany()
            .HasForeignKey(ab => ab.BrandId);

        builder.Entity<ProcessTemplateBrand>()
            .HasKey(ptb => new { ptb.ProcessTemplateId, ptb.BrandId });
        builder.Entity<ProcessTemplateBrand>()
            .HasOne(ptb => ptb.ProcessTemplate)
            .WithMany(pt => pt.AffectedBrands)
            .HasForeignKey(ptb => ptb.ProcessTemplateId);
        builder.Entity<ProcessTemplateBrand>()
            .HasOne(ptb => ptb.Brand)
            .WithMany()
            .HasForeignKey(ptb => ptb.BrandId);

        builder.Entity<UpdateBrand>().HasKey(ub => new { ub.UpdateId, ub.BrandId });
        builder.Entity<UpdateBrand>()
            .HasOne(ub => ub.Update)
            .WithMany(u => u.AffectedBrands)
            .HasForeignKey(ub => ub.UpdateId);
        builder.Entity<UpdateBrand>()
            .HasOne(ub => ub.Brand)
            .WithMany()
            .HasForeignKey(ub => ub.BrandId);

        // Schedule
        builder.Entity<AgentSchedule>()
            .HasOne(s => s.Agent)
            .WithMany()
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<AgentSchedule>()
            .HasOne(s => s.ShiftTemplate)
            .WithMany()
            .HasForeignKey(s => s.ShiftTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<WeekendShiftOffer>()
            .HasOne(o => o.OfferedToAgent)
            .WithMany()
            .HasForeignKey(o => o.OfferedToAgentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<WeekendShiftOffer>()
            .HasOne(o => o.CreatedByLeader)
            .WithMany()
            .HasForeignKey(o => o.CreatedByLeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TimeOffRequest>()
            .HasOne(r => r.Agent)
            .WithMany()
            .HasForeignKey(r => r.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TimeOffRequest>()
            .HasOne(r => r.ReviewedByLeader)
            .WithMany()
            .HasForeignKey(r => r.ReviewedByLeaderId)
            .OnDelete(DeleteBehavior.NoAction);

        // Performance
        builder.Entity<PerformanceReview>()
            .HasOne(r => r.Agent)
            .WithMany()
            .HasForeignKey(r => r.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PerformanceReview>()
            .HasOne(r => r.ReviewedByLeader)
            .WithMany()
            .HasForeignKey(r => r.ReviewedByLeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ReviewItem>()
            .HasOne(i => i.Review)
            .WithMany(r => r.Items)
            .HasForeignKey(i => i.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        // Vault
        builder.Entity<VaultEntry>()
            .HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<VaultEntry>()
            .HasOne(e => e.Category)
            .WithMany(c => c.Entries)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VaultEntry>()
            .HasOne(e => e.ProvisionedByUser)
            .WithMany()
            .HasForeignKey(e => e.ProvisionedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<VaultCategory>()
            .HasOne(c => c.CreatedByUser)
            .WithMany()
            .HasForeignKey(c => c.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<VaultAccessLog>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<VaultAccessLog>()
            .HasOne(l => l.Entry)
            .WithMany()
            .HasForeignKey(l => l.EntryId)
            .OnDelete(DeleteBehavior.NoAction);

        // Seed default vault categories
        builder.Entity<VaultCategory>().HasData(
            new VaultCategory { Id = 1, Name = "CRM",        IconCssClass = "bx bx-data",      IsCustom = false },
            new VaultCategory { Id = 2, Name = "Quemetrics", IconCssClass = "bx bx-bar-chart", IsCustom = false },
            new VaultCategory { Id = 3, Name = "Redmine",    IconCssClass = "bx bx-task",      IsCustom = false }
        );

        // Reports
        builder.Entity<TeamReport>()
            .HasOne(r => r.Team)
            .WithMany()
            .HasForeignKey(r => r.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TeamReport>()
            .HasOne(r => r.ReportedByLeader)
            .WithMany()
            .HasForeignKey(r => r.ReportedByLeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AgentStat>()
            .HasOne(s => s.Report)
            .WithMany(r => r.AgentStats)
            .HasForeignKey(s => s.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AgentStat>()
            .HasOne(s => s.Agent)
            .WithMany()
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FTDLanguageStat>()
            .HasOne(f => f.Report)
            .WithMany(r => r.FTDLanguageStats)
            .HasForeignKey(f => f.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        // Attendance
        builder.Entity<AttendanceLog>()
            .HasOne(a => a.Agent)
            .WithMany()
            .HasForeignKey(a => a.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AttendanceLog>()
            .HasOne(a => a.ReviewedBy)
            .WithMany()
            .HasForeignKey(a => a.ReviewedById)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<AttendanceLog>()
            .Property(a => a.PayType)
            .HasConversion<string>();

        builder.Entity<AttendanceLog>()
            .Property(a => a.Status)
            .HasConversion<string>();

        builder.Entity<AppUser>()
            .Property(u => u.HourlyRate)
            .HasPrecision(10, 2);

        // CS Live Allocation — keep original table names so no migration is needed
        builder.Entity<CsLiveAllocationSlot>().ToTable("CsLiveHelpSlots");
        builder.Entity<CsLiveAllocationSwapLog>().ToTable("CsLiveHelpSwapLogs");

        builder.Entity<CsLiveAllocationSlot>()
            .HasOne(s => s.Agent1)
            .WithMany()
            .HasForeignKey(s => s.Agent1Id)
            .OnDelete(DeleteBehavior.NoAction);
        builder.Entity<CsLiveAllocationSlot>()
            .HasOne(s => s.Agent2)
            .WithMany()
            .HasForeignKey(s => s.Agent2Id)
            .OnDelete(DeleteBehavior.NoAction);
        builder.Entity<CsLiveAllocationSlot>()
            .HasOne(s => s.CreatedBy)
            .WithMany()
            .HasForeignKey(s => s.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CsLiveAllocationSwapLog>()
            .HasOne(l => l.Slot)
            .WithMany()
            .HasForeignKey(l => l.SlotId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<CsLiveAllocationSwapLog>()
            .HasOne(l => l.PreviousAgent)
            .WithMany()
            .HasForeignKey(l => l.PreviousAgentId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.Entity<CsLiveAllocationSwapLog>()
            .HasOne(l => l.NewAgent)
            .WithMany()
            .HasForeignKey(l => l.NewAgentId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.Entity<CsLiveAllocationSwapLog>()
            .HasOne(l => l.ChangedBy)
            .WithMany()
            .HasForeignKey(l => l.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);

        // POI Simulations
        builder.Entity<PoiSimulation>()
            .HasOne(p => p.Brand)
            .WithMany()
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PoiSimulation>()
            .HasOne(p => p.LoggedBy)
            .WithMany()
            .HasForeignKey(p => p.LoggedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PoiSimulation>()
            .HasOne(p => p.ReceivedBy)
            .WithMany()
            .HasForeignKey(p => p.ReceivedById)
            .OnDelete(DeleteBehavior.NoAction);

        // ── Performance indexes ───────────────────────────────────────────

        builder.Entity<Update>()
            .HasIndex(u => new { u.IsArchived, u.IsPinned, u.CreatedAt });

        builder.Entity<AgentSchedule>()
            .HasIndex(s => new { s.AgentId, s.Date });

        builder.Entity<AttendanceLog>()
            .HasIndex(a => new { a.AgentId, a.WorkDate });

        builder.Entity<PerformanceReview>()
            .HasIndex(r => new { r.AgentId, r.ReviewDate });

        builder.Entity<TeamReport>()
            .HasIndex(r => new { r.PeriodType, r.PeriodStart });

        builder.Entity<VaultEntry>()
            .HasIndex(e => new { e.OwnerId, e.CategoryId });

        builder.Entity<PoiSimulation>()
            .HasIndex(p => new { p.LoggedById, p.SimulatedAt });

        builder.Entity<CsLiveAllocationSlot>()
            .HasIndex(s => s.Date);

        builder.Entity<WorkDistribution>()
            .HasIndex(w => w.Date);

        // CS Live Help
        builder.Entity<CsRequest>()
            .Property(r => r.Status)
            .HasConversion<string>();

        builder.Entity<CsRequestArchive>()
            .Property(r => r.Status)
            .HasConversion<string>();

        builder.Entity<CsRequest>()
            .HasOne(r => r.AccountManager)
            .WithMany()
            .HasForeignKey(r => r.AccountManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CsRequest>()
            .HasOne(r => r.Brand)
            .WithMany()
            .HasForeignKey(r => r.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CsRequest>()
            .HasOne(r => r.RequestType)
            .WithMany()
            .HasForeignKey(r => r.RequestTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CsRequestComment>()
            .HasOne(c => c.Request)
            .WithMany(r => r.Comments)
            .HasForeignKey(c => c.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CsRequestComment>()
            .HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CsRequestArchive>()
            .HasOne(r => r.AccountManager)
            .WithMany()
            .HasForeignKey(r => r.AccountManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CsRequestArchive>()
            .HasOne(r => r.Brand)
            .WithMany()
            .HasForeignKey(r => r.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CsRequestArchive>()
            .HasOne(r => r.RequestType)
            .WithMany()
            .HasForeignKey(r => r.RequestTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed default CsRequestType lookup rows
        builder.Entity<CsRequestType>().HasData(
            new CsRequestType { Id = 1, Name = "Simulate POI",   IsOther = false },
            new CsRequestType { Id = 2, Name = "Reset Password", IsOther = false },
            new CsRequestType { Id = 3, Name = "Other",          IsOther = true  }
        );

        // CS Live Help indexes
        builder.Entity<CsRequest>()
            .HasIndex(r => r.Status);
        builder.Entity<CsRequest>()
            .HasIndex(r => r.CreatedAt);
        builder.Entity<CsRequest>()
            .HasIndex(r => r.BrandId);
        builder.Entity<CsRequest>()
            .HasIndex(r => r.AccountManagerId);

        builder.Entity<AmAuditLog>()
            .HasIndex(a => new { a.UserId, a.Timestamp });

        builder.Entity<ActivityLog>()
            .HasIndex(a => a.UserId);
        builder.Entity<ActivityLog>()
            .HasIndex(a => a.Timestamp);
        builder.Entity<ActivityLog>()
            .HasIndex(a => a.Path);

        builder.Entity<ErrorLog>()
            .HasIndex(e => e.Timestamp);

        builder.Entity<DashboardWidget>()
            .HasIndex(w => w.UserId);

        // Reverse-lookup indexes for junction tables
        builder.Entity<AgentTeam>()
            .HasIndex(at => at.TeamId);

        builder.Entity<AgentBrand>()
            .HasIndex(ab => ab.BrandId);

        // ── Global query filters (soft deletes) ──────────────────────────

        builder.Entity<Update>()
            .HasQueryFilter(u => !u.IsArchived);

        builder.Entity<UpdateBrand>()
            .HasQueryFilter(ub => !ub.Update.IsArchived);
    }
}
