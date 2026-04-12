using CongNoGolden.Application.Backups;
using CongNoGolden.Infrastructure.Services;

namespace CongNoGolden.Api.Services;

public sealed class BackupWorkerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackupQueue _queue;
    private readonly ILogger<BackupWorkerHostedService> _logger;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    public BackupWorkerHostedService(
        IServiceScopeFactory scopeFactory,
        BackupQueue queue,
        ILogger<BackupWorkerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var jobId))
            {
                await ProcessQueuedJobAsync(jobId, stoppingToken);
                continue;
            }

            if (await ProcessPendingBackupJobAsync(stoppingToken))
            {
                continue;
            }

            if (await ProcessPendingOffsiteUploadAsync(stoppingToken))
            {
                continue;
            }

            await Task.Delay(IdleDelay, stoppingToken);
        }
    }

    private async Task ProcessQueuedJobAsync(Guid jobId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IBackupService>();
            await service.ProcessJobAsync(jobId, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup worker failed for job {JobId}.", jobId);
        }
    }

    private async Task<bool> ProcessPendingBackupJobAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IBackupService>();
            return await service.ProcessNextPendingJobAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup worker failed while polling pending backup jobs.");
            return false;
        }
    }

    private async Task<bool> ProcessPendingOffsiteUploadAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IBackupOffsiteService>();
            return await service.ProcessNextPendingUploadAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup worker failed while polling pending offsite uploads.");
            return false;
        }
    }
}
