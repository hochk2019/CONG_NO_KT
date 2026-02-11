using CongNoGolden.Application.Backups;

namespace CongNoGolden.Api.Services;

public sealed class BackupSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupSchedulerHostedService> _logger;

    public BackupSchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackupSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IBackupService>();
                var settings = await service.GetSettingsAsync(stoppingToken);
                if (!settings.Enabled)
                {
                    continue;
                }

                if (!TimeSpan.TryParse(settings.ScheduleTime, out var scheduleTime))
                {
                    _logger.LogWarning("Backup schedule time invalid.");
                    continue;
                }

                var timezone = ResolveTimezone(settings.Timezone);
                var now = DateTimeOffset.UtcNow;
                var nextRun = BackupScheduleCalculator.GetNextRunAt(
                    now,
                    (DayOfWeek)settings.ScheduleDayOfWeek,
                    scheduleTime,
                    timezone);

                if (settings.LastRunAt.HasValue && settings.LastRunAt.Value >= nextRun)
                {
                    continue;
                }

                if (await service.HasPendingScheduledBackupAsync(stoppingToken))
                {
                    continue;
                }

                if (now >= nextRun)
                {
                    await service.EnqueueScheduledBackupAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup scheduler tick failed.");
            }
        }
    }

    private static TimeZoneInfo ResolveTimezone(string timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch
        {
            return TimeZoneInfo.Local;
        }
    }
}
