using CongNoGolden.Application.Backups;
using CongNoGolden.Application.Common;

namespace CongNoGolden.Infrastructure.Services;

public sealed class NullBackupOffsiteService : IBackupOffsiteService
{
    public static NullBackupOffsiteService Instance { get; } = new();

    private static readonly BackupOffsiteConnectionStatus DisconnectedStatus =
        new(false, "google_drive", null, null, null, null);

    private NullBackupOffsiteService()
    {
    }

    public Task<string> BuildGoogleDriveConnectUrlAsync(string redirectUri, string? folderId, CancellationToken ct) =>
        Task.FromResult(string.Empty);

    public Task<BackupOffsiteConnectionStatus> CompleteGoogleDriveCallbackAsync(string code, string redirectUri, string? folderId, CancellationToken ct) =>
        Task.FromResult(DisconnectedStatus);

    public Task DisconnectAsync(CancellationToken ct) => Task.CompletedTask;

    public Task EnqueueUploadForBackupJobAsync(Guid backupJobId, CancellationToken ct) => Task.CompletedTask;

    public Task<BackupOffsiteConnectionStatus> GetConnectionStatusAsync(CancellationToken ct) =>
        Task.FromResult(DisconnectedStatus);

    public Task<PagedResult<BackupOffsiteUploadDto>> ListUploadsAsync(int page, int pageSize, CancellationToken ct) =>
        Task.FromResult(new PagedResult<BackupOffsiteUploadDto>(Array.Empty<BackupOffsiteUploadDto>(), page, pageSize, 0));

    public Task<bool> ProcessNextPendingUploadAsync(CancellationToken ct) => Task.FromResult(false);

    public Task<BackupOffsiteUploadDto> ReuploadAsync(Guid backupJobId, CancellationToken ct) =>
        throw new InvalidOperationException("Offsite backup is not configured.");

    public Task<BackupOffsiteUploadDto> TestUploadAsync(CancellationToken ct) =>
        throw new InvalidOperationException("Offsite backup is not configured.");
}
