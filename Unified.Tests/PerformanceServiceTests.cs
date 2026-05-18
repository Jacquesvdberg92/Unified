using Unified.Models.Performance;
using Unified.Models.Identity;
using Unified.Services;
using Unified.Tests.Helpers;

namespace Unified.Tests;

public class PerformanceServiceTests
{
    private static PerformanceService BuildSvc(string name)
        => new PerformanceService(DbHelper.CreateInMemory(name));

    // ── Rating validation ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task CreateReview_RejectsOutOfRangeRating(int badRating)
    {
        var svc = BuildSvc($"{nameof(CreateReview_RejectsOutOfRangeRating)}_{badRating}");

        var review = new PerformanceReview
        {
            AgentId            = "a1",
            ReviewedByLeaderId = "l1",
            ReviewDate         = DateTime.Today,
            Items = new List<ReviewItem>
            {
                new ReviewItem { Category = ReviewCategory.Chat, ReferenceId = "C1", Rating = badRating }
            }
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => svc.CreateReviewAsync(review));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task CreateReview_AcceptsValidRatings(int validRating)
    {
        var db  = DbHelper.CreateInMemory($"{nameof(CreateReview_AcceptsValidRatings)}_{validRating}");
        var svc = new PerformanceService(db);

        var review = new PerformanceReview
        {
            AgentId            = "a1",
            ReviewedByLeaderId = "l1",
            ReviewDate         = DateTime.Today,
            Items = new List<ReviewItem>
            {
                new ReviewItem { Category = ReviewCategory.Ticket, ReferenceId = "T1", Rating = validRating }
            }
        };

        var saved = await svc.CreateReviewAsync(review);
        Assert.True(saved.Id > 0);
    }

    // ── Average calculation ───────────────────────────────────────────────

    [Fact]
    public async Task GetAverageRating_ReturnsCorrectMean()
    {
        var db  = DbHelper.CreateInMemory(nameof(GetAverageRating_ReturnsCorrectMean));
        var svc = new PerformanceService(db);

        var review = new PerformanceReview
        {
            AgentId            = "a1",
            ReviewedByLeaderId = "l1",
            ReviewDate         = DateTime.Today,
            Items = new List<ReviewItem>
            {
                new ReviewItem { Category = ReviewCategory.Chat, ReferenceId = "C1", Rating = 6 },
                new ReviewItem { Category = ReviewCategory.Chat, ReferenceId = "C2", Rating = 8 }
            }
        };
        await svc.CreateReviewAsync(review);

        var avg = await svc.GetAverageRatingAsync("a1");

        Assert.NotNull(avg);
        Assert.Equal(7.0, avg!.Value, precision: 1);
    }

    [Fact]
    public async Task GetAverageRating_ReturnsNull_WhenNoReviews()
    {
        var svc = BuildSvc(nameof(GetAverageRating_ReturnsNull_WhenNoReviews));
        var avg = await svc.GetAverageRatingAsync("unknown-agent");
        Assert.Null(avg);
    }
}
