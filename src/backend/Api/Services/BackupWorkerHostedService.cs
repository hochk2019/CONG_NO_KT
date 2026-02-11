using CongNoGolden.Application.Backups;
using CongNoGolden.Infrastructure.Services;

namespace CongNoGolden.Api.Services;

public sealed class BackupWorkerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackupQueue _queue;
    private readonly ILogger<BackupWorkerHostedService> _logger;

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
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
