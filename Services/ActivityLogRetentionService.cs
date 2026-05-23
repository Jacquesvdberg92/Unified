using Microsoft.EntityFrameworkCore;
using Unified.Data;

namespace Unified.Services;

public class ActivityLogRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityLogRetentionService> _logger;
    private readonly TimeSpan _interval;
    private readonly int _retentionDays;

    public ActivityLogRetentionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ActivityLogRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromHours(config.GetValue("ActivityLogging:Retention:RunEveryHours", 24));
        _retentionDays = config.GetValue("ActivityLogging:Retention:Days", 60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
                var deleted = await db.ActivityLogs
                    .Where(x => x.Timestamp < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation("Activity log retention removed {Count} rows older than {Days} days.", deleted, _retentionDays);
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Activity log retention pass failed.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}