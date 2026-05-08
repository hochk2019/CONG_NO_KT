using System.Collections.Concurrent;
using CongNoGolden.Api.Services;
using CongNoGolden.Application.Backups;
using CongNoGolden.Application.Common;
using CongNoGolden.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class BackupWorkerHostedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenQueueHasJob_ProcessesQueuedJobBeforePollingFallbacks()
    {
        var events = new ConcurrentQueue<string>();
        var queue = new BackupQueue();
        var queuedJobId = Guid.NewGuid();
        queue.Enqueue(queuedJobId);

        var backupService = new StubBackupService(events)
        {
            ProcessNextPendingJobResult = false,
        };
        var offsiteService = new StubBackupOffsiteService(events)
        {
            ProcessNextPendingUploadResult = false,
        };

        using var worker = CreateWorker(queue, backupService, offsiteService);

        await worker.StartAsync(CancellationToken.None);
        await backupService.WaitForQueuedJobAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(new[] { queuedJobId }, backupService.ProcessedJobIds);
        Assert.Equal($"queued:{queuedJobId}", events.TryPeek(out var firstEvent) ? firstEvent : null);
    }

    [Fact]
    public async Task ExecuteAsync_WhenQueueIsEmpty_ProcessesPendingBackupJobBeforeOffsite()
    {
        var events = new ConcurrentQueue<string>();
        var backupService = new StubBackupService(events)
        {
            ProcessNextPendingJobResult = true,
        };
        var offsiteService = new StubBackupOffsiteService(events)
        {
            ProcessNextPendingUploadResult = true,
        };

        using var worker = CreateWorker(new BackupQueue(), backupService, offsiteService);

        await worker.StartAsync(CancellationToken.None);
        await backupService.WaitForPendingJobPollAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.True(backupService.ProcessNextPendingJobCalls >= 1);
        Assert.Equal("pending-backup", events.TryPeek(out var firstEvent) ? firstEvent : null);
        Assert.Equal(0, offsiteService.ProcessNextPendingUploadCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPendingBackupJob_ProcessesPendingOffsiteUpload()
    {
        var events = new ConcurrentQueue<string>();
        var backupService = new StubBackupService(events)
        {
            ProcessNextPendingJobResult = false,
        };
        var offsiteService = new StubBackupOffsiteService(events)
        {
            ProcessNextPendingUploadResult = true,
        };

        using var worker = CreateWorker(new BackupQueue(), backupService, offsiteService);

        await worker.StartAsync(CancellationToken.None);
        await offsiteService.WaitForPendingUploadPollAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.True(backupService.ProcessNextPendingJobCalls >= 1);
        Assert.True(offsiteService.ProcessNextPendingUploadCalls >= 1);

        var orderedEvents = events.ToArray();
        Assert.True(orderedEvents.Length >= 2);
        Assert.Equal("pending-backup", orderedEvents[0]);
        Assert.Equal("pending-offsite", orderedEvents[1]);
    }

    private static BackupWorkerHostedService CreateWorker(
        BackupQueue queue,
        StubBackupService backupService,
        StubBackupOffsiteService offsiteService)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBackupService>(backupService);
        services.AddSingleton<IBackupOffsiteService>(offsiteService);

        return new BackupWorkerHostedService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            queue,
            NullLogger<BackupWorkerHostedService>.Instance);
    }

    private sealed class StubBackupService : IBackupService
    {
        private readonly ConcurrentQueue<string> _events;
        private readonly ConcurrentQueue<Guid> _processedJobIds = new();
        private readonly TaskCompletionSource<bool> _queuedJobProcessed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _pendingJobPolled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public StubBackupService(ConcurrentQueue<string>? events = null)
        {
            _events = events ?? new ConcurrentQueue<string>();
        }

        public IReadOnlyCollection<Guid> ProcessedJobIds => _processedJobIds.ToArray();

        public bool ProcessNextPendingJobResult { get; init; }

        public int ProcessNextPendingJobCalls { get; private set; }

        public Task WaitForQueuedJobAsync() => _queuedJobProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public Task WaitForPendingJobPollAsync() => _pendingJobPolled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public Task<BackupSettingsDto> GetSettingsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<BackupSettingsDto> UpdateSettingsAsync(BackupSettingsUpdateRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<BackupJobListItem> EnqueueManualBackupAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<BackupJobListItem>> ListJobsAsync(BackupJobQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<BackupJobDetail?> GetJobAsync(Guid jobId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BackupDownloadToken> IssueDownloadTokenAsync(Guid jobId, DateTimeOffset now, TimeSpan ttl, CancellationToken ct) => throw new NotSupportedException();
        public Task<Stream?> OpenDownloadStreamAsync(Guid jobId, string token, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<BackupUploadResult> UploadAsync(string fileName, long fileSize, Stream stream, CancellationToken ct) => throw new NotSupportedException();
        public Task RestoreAsync(BackupRestoreRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<BackupAuditItem>> ListAuditAsync(int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsMaintenanceModeAsync(CancellationToken ct) => Task.FromResult(false);
        public Task<bool> HasPendingScheduledBackupAsync(CancellationToken ct) => Task.FromResult(false);
        public Task EnqueueScheduledBackupAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<bool> ProcessNextPendingJobAsync(CancellationToken ct)
        {
            ProcessNextPendingJobCalls++;
            _events.Enqueue("pending-backup");
            _pendingJobPolled.TrySetResult(true);
            return Task.FromResult(ProcessNextPendingJobResult);
        }

        public Task ProcessJobAsync(Guid jobId, CancellationToken ct)
        {
            _processedJobIds.Enqueue(jobId);
            _events.Enqueue($"queued:{jobId}");
            _queuedJobProcessed.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    private sealed class StubBackupOffsiteService : IBackupOffsiteService
    {
        private readonly ConcurrentQueue<string> _events;
        private readonly TaskCompletionSource<bool> _pendingUploadPolled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public StubBackupOffsiteService(ConcurrentQueue<string>? events = null)
        {
            _events = events ?? new ConcurrentQueue<string>();
        }

        public bool ProcessNextPendingUploadResult { get; init; }

        public int ProcessNextPendingUploadCalls { get; private set; }

        public Task WaitForPendingUploadPollAsync() => _pendingUploadPolled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public Task<string> BuildGoogleDriveConnectUrlAsync(string redirectUri, string? folderId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BackupOffsiteConnectionStatus> CompleteGoogleDriveCallbackAsync(string code, string redirectUri, string? folderId, CancellationToken ct) => throw new NotSupportedException();
        public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;
        public Task EnqueueUploadForBackupJobAsync(Guid backupJobId, CancellationToken ct) => Task.CompletedTask;
        public Task<BackupOffsiteConnectionStatus> GetConnectionStatusAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<BackupOffsiteUploadDto>> ListUploadsAsync(int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> ProcessNextPendingUploadAsync(CancellationToken ct)
        {
            ProcessNextPendingUploadCalls++;
            _events.Enqueue("pending-offsite");
            _pendingUploadPolled.TrySetResult(true);
            return Task.FromResult(ProcessNextPendingUploadResult);
        }

        public Task<BackupOffsiteUploadDto> ReuploadAsync(Guid backupJobId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BackupOffsiteUploadDto> TestUploadAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
