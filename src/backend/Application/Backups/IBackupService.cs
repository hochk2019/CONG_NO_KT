using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Backups;

public interface IBackupService
{
    Task<BackupSettingsDto> GetSettingsAsync(CancellationToken ct);
    Task<BackupSettingsDto> UpdateSettingsAsync(BackupSettingsUpdateRequest request, CancellationToken ct);
    Task<BackupJobListItem> EnqueueManualBackupAsync(CancellationToken ct);
    Task<PagedResult<BackupJobListItem>> ListJobsAsync(BackupJobQuery query, CancellationToken ct);
    Task<BackupJobDetail?> GetJobAsync(Guid jobId, CancellationToken ct);
    Task<BackupDownloadToken> IssueDownloadTokenAsync(Guid jobId, DateTimeOffset now, TimeSpan ttl, CancellationToken ct);
    Task<Stream?> OpenDownloadStreamAsync(Guid jobId, string token, DateTimeOffset now, CancellationToken ct);
    Task<BackupUploadResult> UploadAsync(string fileName, long fileSize, Stream stream, CancellationToken ct);
    Task RestoreAsync(BackupRestoreRequest request, CancellationToken ct);
    Task<PagedResult<BackupAuditItem>> ListAuditAsync(int page, int pageSize, CancellationToken ct);
    Task<bool> IsMaintenanceModeAsync(CancellationToken ct);

    Task<bool> HasPendingScheduledBackupAsync(CancellationToken ct);
    Task EnqueueScheduledBackupAsync(CancellationToken ct);
    Task ProcessJobAsync(Guid jobId, CancellationToken ct);
}
