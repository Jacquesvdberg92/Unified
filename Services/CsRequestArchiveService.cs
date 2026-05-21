using Microsoft.EntityFrameworkCore;
using Unified.Data;
using Unified.Models.CsLiveHelp;

namespace Unified.Services;

/// <summary>
/// Background service that periodically moves old <c>Completed</c> <see cref="CsRequest"/> records
/// into <see cref="CsRequestArchive"/>.
///
/// Configuration (appsettings.json section <c>CsLiveHelp:Archive</c>):
///   RunEveryHours            — how often the service runs (default: 6)
///   CompleteAgeThresholdDays — how old a Completed card must be before archiving (default: 3)
/// </summary>
public class CsRequestArchiveService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CsRequestArchiveService> _logger;
    private readonly TimeSpan _interval;
    private readonly int _thresholdDays;

    public CsRequestArchiveService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<CsRequestArchiveService> logger)
    {
        _scopeFactory  = scopeFactory;
        _logger        = logger;
        _interval      = TimeSpan.FromHours(config.GetValue("CsLiveHelp:Archive:RunEveryHours", 6));
        _thresholdDays = config.GetValue("CsLiveHelp:Archive:CompleteAgeThresholdDays", 3);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "CsRequestArchiveService started — interval {Interval}h, threshold {Days}d.",
            _interval.TotalHours, _thresholdDays);

        // Stagger first run by 30 s so the app is fully started.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunArchivePassAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunArchivePassAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoff = DateTime.UtcNow.AddDays(-_thresholdDays);

            var candidates = await db.CsRequests
                .Where(r => r.Status == CsRequestStatus.Completed && r.UpdatedAt <= cutoff)
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                _logger.LogDebug("CsRequestArchiveService: no records to archive.");
                return;
            }

            var archives = candidates.Select(r => new CsRequestArchive
            {
                OriginalRequestId = r.Id,
                AccountManagerId  = r.AccountManagerId,
                IsInternal        = r.IsInternal,
                BrandId           = r.BrandId,
                RequestTypeId     = r.RequestTypeId,
                CustomDescription = r.CustomDescription,
                AssignedToId      = r.AssignedToId,
                Status            = r.Status,
                CreatedAt         = r.CreatedAt,
                UpdatedAt         = r.UpdatedAt,
                ArchivedAt        = DateTime.UtcNow
            }).ToList();

            await db.CsRequestArchives.AddRangeAsync(archives, ct);
            db.CsRequests.RemoveRange(candidates);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "CsRequestArchiveService: archived {Count} request(s) older than {Days} day(s).",
                candidates.Count, _thresholdDays);
        }
        catch (OperationCanceledException) { /* shutting down — expected */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CsRequestArchiveService encountered an error during archive pass.");
        }
    }
}
