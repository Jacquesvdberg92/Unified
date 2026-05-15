using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;
using Unified.Models.ProcessTemplates;
using Unified.Models.Schedule;
using Unified.Models.Updates;

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
            .OnDelete(DeleteBehavior.SetNull);
    }
}
