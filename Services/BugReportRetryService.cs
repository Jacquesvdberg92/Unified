namespace Unified.Services;

/// <summary>Periodically resends bug reports that couldn't reach GitHub when they were submitted.</summary>
public class BugReportRetryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BugReportRetryService> _logger;
    private readonly TimeSpan _interval;

    public BugReportRetryService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<BugReportRetryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(config.GetValue("GitHub:RetryEveryMinutes", 15));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var bugReports = scope.ServiceProvider.GetRequiredService<BugReportService>();

                var sent = await bugReports.RetryPendingAsync(stoppingToken);
                if (sent > 0)
                {
                    _logger.LogInformation("Bug report retry sent {Count} pending reports to GitHub.", sent);
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bug report retry pass failed.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
