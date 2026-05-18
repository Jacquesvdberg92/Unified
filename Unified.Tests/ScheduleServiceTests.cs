using Unified.Models.Identity;
using Unified.Models.Schedule;
using Unified.Services;
using Unified.Tests.Helpers;

namespace Unified.Tests;

public class ScheduleServiceTests
{
    private static async Task<(Unified.Data.AppDbContext db, ScheduleService svc, Team team, AppUser agent)> BuildAsync(string name)
    {
        var db = DbHelper.CreateInMemory(name);

        var team  = new Team { Name = "Alpha" };
        db.Teams.Add(team);

        var agent = new AppUser { Id = "agent-1", UserName = "agent1", DisplayName = "Agent One", HasWeekendShift = false, IsSwissArmyKnife = false };
        db.Users.Add(agent);
        await db.SaveChangesAsync();

        db.AgentTeams.Add(new AgentTeam { AgentId = agent.Id, TeamId = team.Id });
        await db.SaveChangesAsync();

        return (db, new ScheduleService(db), team, agent);
    }

    // ── Weekend eligibility ───────────────────────────────────────────────

    [Fact]
    public async Task EligibleAgents_ExcludesHasWeekendShift()
    {
        var (db, svc, _, _) = await BuildAsync(nameof(EligibleAgents_ExcludesHasWeekendShift));

        // Add an agent who already has a fixed weekend shift
        db.Users.Add(new AppUser { Id = "agent-2", UserName = "agent2", DisplayName = "Weekend Agent", HasWeekendShift = true, IsSwissArmyKnife = false });
        await db.SaveChangesAsync();

        var eligible = await svc.GetAgentsEligibleForWeekendAsync();

        Assert.All(eligible, u => Assert.False(u.HasWeekendShift));
        Assert.DoesNotContain(eligible, u => u.Id == "agent-2");
    }

    [Fact]
    public async Task SpinWheelCandidates_ExcludesAlreadyOfferedThisWeek()
    {
        var (db, svc, _, agent) = await BuildAsync(nameof(SpinWheelCandidates_ExcludesAlreadyOfferedThisWeek));

        var weekStart = new DateTime(2025, 1, 6); // a Monday
        db.WeekendShiftOffers.Add(new WeekendShiftOffer
        {
            WeekStartDate    = weekStart,
            OfferedToAgentId = agent.Id,
            CreatedByLeaderId = "leader-1"
        });
        await db.SaveChangesAsync();

        var candidates = await svc.SpinWheelCandidatesAsync(weekStart);

        Assert.DoesNotContain(candidates, u => u.Id == agent.Id);
    }

    // ── SAK union ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWeeklySchedule_IncludesSAKAgents()
    {
        var (db, svc, team, _) = await BuildAsync(nameof(GetWeeklySchedule_IncludesSAKAgents));

        var sak = new AppUser { Id = "sak-1", UserName = "sak1", DisplayName = "Swiss Knife", IsSwissArmyKnife = true, HasWeekendShift = false };
        db.Users.Add(sak);
        var weekStart = new DateTime(2025, 1, 6);
        db.AgentSchedules.Add(new AgentSchedule { AgentId = sak.Id, Date = weekStart, Type = ScheduleEntryType.Regular });
        await db.SaveChangesAsync();

        var schedule = await svc.GetWeeklyScheduleAsync(team.Id, weekStart);

        Assert.Contains(schedule, s => s.AgentId == sak.Id);
    }

    // ── Time-off overlap guard ─────────────────────────────────────────────

    [Fact]
    public async Task SubmitTimeOff_ThrowsOnOverlappingPending()
    {
        var (db, svc, _, agent) = await BuildAsync(nameof(SubmitTimeOff_ThrowsOnOverlappingPending));

        var req1 = new TimeOffRequest
        {
            AgentId   = agent.Id,
            StartDate = new DateTime(2025, 2, 10),
            EndDate   = new DateTime(2025, 2, 14),
            Type      = TimeOffType.Vacation
        };
        await svc.SubmitTimeOffRequestAsync(req1);

        var req2 = new TimeOffRequest
        {
            AgentId   = agent.Id,
            StartDate = new DateTime(2025, 2, 12),
            EndDate   = new DateTime(2025, 2, 16),
            Type      = TimeOffType.Vacation
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SubmitTimeOffRequestAsync(req2));
    }
}
