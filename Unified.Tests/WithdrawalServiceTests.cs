using Microsoft.EntityFrameworkCore;
using Unified.Models.EmailTemplates;
using Unified.Models.Identity;
using Unified.Models.Withdrawals;
using Unified.Services;
using Unified.Tests.Helpers;

namespace Unified.Tests;

public class WithdrawalServiceTests
{
    private static async Task<(Unified.Data.AppDbContext db, WithdrawalService svc, Brand brand)> BuildAsync(string name)
    {
        var db = DbHelper.CreateInMemory(name);

        var brand = new Brand { Name = "Brand A" };
        db.Brands.Add(brand);
        db.Users.Add(new AppUser { Id = "u1", UserName = "u1", DisplayName = "User One" });
        db.Users.Add(new AppUser { Id = "u2", UserName = "u2", DisplayName = "User Two" });
        await db.SaveChangesAsync();

        return (db, new WithdrawalService(db), brand);
    }

    private static Task<WithdrawalRequest> CreateAsync(WithdrawalService svc, Brand brand, bool pickUp = false,
        WithdrawalDocuments missing = WithdrawalDocuments.None) =>
        svc.CreateAsync("u1", " CL123 ", brand.Id, 100m, "eur", missing, null, null, pickUp);

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_TrimsClientId_NormalisesCurrency_AndLogsCreation()
    {
        var (db, svc, brand) = await BuildAsync(nameof(Create_TrimsClientId_NormalisesCurrency_AndLogsCreation));

        var wd = await CreateAsync(svc, brand);

        Assert.Equal("CL123", wd.ClientId);
        Assert.Equal("EUR", wd.Currency);
        Assert.Equal(WithdrawalStatus.Open, wd.Status);
        Assert.Null(wd.AssignedToId);
        Assert.Single(await db.WithdrawalComments.Where(c => c.WithdrawalId == wd.Id && c.IsSystemMessage).ToListAsync());
    }

    [Fact]
    public async Task Create_WithPickUp_AssignsCreatorAndStartsInProgress()
    {
        var (_, svc, brand) = await BuildAsync(nameof(Create_WithPickUp_AssignsCreatorAndStartsInProgress));

        var wd = await CreateAsync(svc, brand, pickUp: true);

        Assert.Equal("u1", wd.AssignedToId);
        Assert.Equal(WithdrawalStatus.InProgress, wd.Status);
    }

    [Fact]
    public async Task Create_DropsCurrencyWhenNoAmount()
    {
        var (_, svc, brand) = await BuildAsync(nameof(Create_DropsCurrencyWhenNoAmount));

        var wd = await svc.CreateAsync("u1", "CL1", brand.Id, null, "EUR", WithdrawalDocuments.None, null, null, false);

        Assert.Null(wd.Amount);
        Assert.Null(wd.Currency);
    }

    // ── Pick up / release ─────────────────────────────────────────────────

    [Fact]
    public async Task PickUp_FailsWhenSomeoneElseHasIt()
    {
        var (db, svc, brand) = await BuildAsync(nameof(PickUp_FailsWhenSomeoneElseHasIt));
        var wd = await CreateAsync(svc, brand);

        Assert.True(await svc.PickUpAsync(wd.Id, "u1"));
        Assert.False(await svc.PickUpAsync(wd.Id, "u2"));

        var saved = await db.WithdrawalRequests.FindAsync(wd.Id);
        Assert.Equal("u1", saved!.AssignedToId);
        Assert.Equal(WithdrawalStatus.InProgress, saved.Status);
    }

    [Fact]
    public async Task Release_OnlyByOwner_AndReturnsToOpen()
    {
        var (db, svc, brand) = await BuildAsync(nameof(Release_OnlyByOwner_AndReturnsToOpen));
        var wd = await CreateAsync(svc, brand, pickUp: true);

        Assert.False(await svc.ReleaseAsync(wd.Id, "u2"));
        Assert.True(await svc.ReleaseAsync(wd.Id, "u1"));

        var saved = await db.WithdrawalRequests.FindAsync(wd.Id);
        Assert.Null(saved!.AssignedToId);
        Assert.Equal(WithdrawalStatus.Open, saved.Status);
    }

    // ── Documents / status / details ──────────────────────────────────────

    [Fact]
    public async Task SetMissingDocuments_LogsReceivedAndMissing()
    {
        var (db, svc, brand) = await BuildAsync(nameof(SetMissingDocuments_LogsReceivedAndMissing));
        var wd = await CreateAsync(svc, brand, missing: WithdrawalDocuments.POI | WithdrawalDocuments.POR);

        await svc.SetMissingDocumentsAsync(wd.Id, WithdrawalDocuments.POR | WithdrawalDocuments.CC, "u2");

        var saved = await db.WithdrawalRequests.FindAsync(wd.Id);
        Assert.Equal(WithdrawalDocuments.POR | WithdrawalDocuments.CC, saved!.MissingDocuments);
        var last = await db.WithdrawalComments.Where(c => c.WithdrawalId == wd.Id).OrderBy(c => c.Id).LastAsync();
        Assert.True(last.IsSystemMessage);
        Assert.Equal("marked POI as received and marked CC as missing", last.Body);
    }

    [Fact]
    public async Task SetStatus_SameStatus_DoesNotLog()
    {
        var (db, svc, brand) = await BuildAsync(nameof(SetStatus_SameStatus_DoesNotLog));
        var wd = await CreateAsync(svc, brand);
        var before = await db.WithdrawalComments.CountAsync();

        Assert.True(await svc.SetStatusAsync(wd.Id, WithdrawalStatus.Open, "u1"));
        Assert.Equal(before, await db.WithdrawalComments.CountAsync());

        Assert.True(await svc.SetStatusAsync(wd.Id, WithdrawalStatus.WaitingOnClient, "u1"));
        Assert.Equal(before + 1, await db.WithdrawalComments.CountAsync());
    }

    [Fact]
    public async Task SetStatus_RejectsUndefinedValue()
    {
        var (_, svc, brand) = await BuildAsync(nameof(SetStatus_RejectsUndefinedValue));
        var wd = await CreateAsync(svc, brand);

        Assert.False(await svc.SetStatusAsync(wd.Id, (WithdrawalStatus)42, "u1"));
    }

    [Fact]
    public async Task UpdateDetails_StoresRelatedTicketsAndAmount()
    {
        var (db, svc, brand) = await BuildAsync(nameof(UpdateDetails_StoresRelatedTicketsAndAmount));
        var wd = await CreateAsync(svc, brand);

        await svc.UpdateDetailsAsync(wd.Id, 250.5m, "usd", "  #123\nhttps://example.test/item  ", "u1");

        var saved = await db.WithdrawalRequests.FindAsync(wd.Id);
        Assert.Equal(250.5m, saved!.Amount);
        Assert.Equal("USD", saved.Currency);
        Assert.Equal("#123\nhttps://example.test/item", saved.RelatedTickets);
    }

    // ── Comments ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AddComment_RejectsBlank()
    {
        var (_, svc, brand) = await BuildAsync(nameof(AddComment_RejectsBlank));
        var wd = await CreateAsync(svc, brand);

        Assert.False(await svc.AddCommentAsync(wd.Id, "u1", "   "));
        Assert.True(await svc.AddCommentAsync(wd.Id, "u1", "Asked for POR"));
    }

    // ── Board ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Board_FiltersByScopeAndMissingDocuments()
    {
        var (_, svc, brand) = await BuildAsync(nameof(Board_FiltersByScopeAndMissingDocuments));
        var mine       = await CreateAsync(svc, brand, pickUp: true);
        var unassigned = await CreateAsync(svc, brand, missing: WithdrawalDocuments.BD);

        Assert.Equal([mine.Id], (await svc.GetBoardAsync("u1", WithdrawalService.Scope.Mine)).Select(w => w.Id));
        Assert.Equal([unassigned.Id], (await svc.GetBoardAsync("u1", WithdrawalService.Scope.Unassigned)).Select(w => w.Id));
        Assert.Equal([unassigned.Id], (await svc.GetBoardAsync("u1", missingOnly: true)).Select(w => w.Id));
    }

    [Fact]
    public async Task Board_HidesOldDone_UnlessSearching()
    {
        var (db, svc, brand) = await BuildAsync(nameof(Board_HidesOldDone_UnlessSearching));
        var wd = await CreateAsync(svc, brand);
        var saved = await db.WithdrawalRequests.FindAsync(wd.Id);
        saved!.Status    = WithdrawalStatus.Done;
        saved.UpdatedAt  = DateTime.UtcNow.AddDays(-30);
        await db.SaveChangesAsync();

        Assert.Empty(await svc.GetBoardAsync("u1"));
        Assert.Single(await svc.GetBoardAsync("u1", search: $"WD-{wd.Id}"));
        Assert.Single(await svc.GetBoardAsync("u1", search: "CL12"));
    }
}
