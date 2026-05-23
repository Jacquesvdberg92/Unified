using System.Threading.Channels;
using Unified.Data;
using Unified.Models.Logging;

namespace Unified.Services;

public interface IActivityLogQueue
{
    ValueTask EnqueueAsync(ActivityLog log, CancellationToken cancellationToken = default);
}

public class ActivityLogQueueService : BackgroundService, IActivityLogQueue
{
    private readonly Channel<ActivityLog> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityLogQueueService> _logger;

    public ActivityLogQueueService(IServiceScopeFactory scopeFactory, ILogger<ActivityLogQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = Channel.CreateUnbounded<ActivityLog>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(ActivityLog log, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(log, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var log in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.ActivityLogs.Add(log);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // app is shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist activity log entry for {Path}.", log.Path);
            }
        }
    }
}