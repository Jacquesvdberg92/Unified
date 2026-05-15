using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;
using Unified.Models.ProcessTemplates;

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
    }
}
