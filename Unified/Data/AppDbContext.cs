using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;

namespace Unified.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<AgentTeam> AgentTeams => Set<AgentTeam>();
    public DbSet<AgentBrand> AgentBrands => Set<AgentBrand>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();

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
    }
}
