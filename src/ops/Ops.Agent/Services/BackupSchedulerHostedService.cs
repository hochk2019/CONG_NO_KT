using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ops.Shared.Config;

namespace Ops.Agent.Services;

public sealed class BackupSchedulerHostedService(
    AgentState state,
    BackupService backupService,
    ILogger<BackupSchedulerHostedService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private DateOnly? _lastRunDate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var config = state.Config;
            var schedule = config.BackupSchedule;
            if (!schedule.Enabled)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                continue;
            }

            if (!BackupScheduleCalculator.TryParseTimeOfDay(schedule.TimeOfDay, out var timeOfDay))
            {
                logger.LogWarning("Invalid backup time configuration: {Time}", schedule.TimeOfDay);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                continue;
            }

            var now = DateTimeOffset.Now;
            var nextRun = BackupScheduleCalculator.GetNextRun(now, timeOfDay);
            var delay = nextRun - now;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);

            config = state.Config;
            schedule = config.BackupSchedule;
            if (!schedule.Enabled)
                continue;

            var today = DateOnly.FromDateTime(DateTime.Now);
            if (_lastRunDate == today)
                continue;

            if (!await _mutex.WaitAsync(0, stoppingToken))
                continue;

            try
            {
                var (file, result) = await backupService.CreateBackupAsync(config, stoppingToken);
                _lastRunDate = today;
                if (result.ExitCode == 0)
                    logger.LogInformation("Backup completed: {File}", file);
                else
                    logger.LogWarning("Backup failed: {Error}", result.Stderr);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled backup failed");
            }
            finally
            {
                _mutex.Release();
            }
        }
    }
}
