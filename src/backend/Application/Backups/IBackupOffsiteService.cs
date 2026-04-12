using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Backups;

public interface IBackupOffsiteService
{
    Task<string> BuildGoogleDriveConnectUrlAsync(string redirectUri, string? folderId, CancellationToken ct);
    Task<BackupOffsiteConnectionStatus> CompleteGoogleDriveCallbackAsync(string code, string redirectUri, string? folderId, CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
    Task EnqueueUploadForBackupJobAsync(Guid backupJobId, CancellationToken ct);
    Task<BackupOffsiteConnectionStatus> GetConnectionStatusAsync(CancellationToken ct);
    Task<PagedResult<BackupOffsiteUploadDto>> ListUploadsAsync(int page, int pageSize, CancellationToken ct);
    Task<bool> ProcessNextPendingUploadAsync(CancellationToken ct);
    Task<BackupOffsiteUploadDto> ReuploadAsync(Guid backupJobId, CancellationToken ct);
    Task<BackupOffsiteUploadDto> TestUploadAsync(CancellationToken ct);
}
