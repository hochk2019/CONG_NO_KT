namespace CongNoGolden.Application.Backups;

public enum BackupScheduleFrequency
{
    Daily = 1,
    Weekly = 2,
}

public sealed record BackupSettingsDto(
    bool Enabled,
    int ScheduleFrequency,
    string BackupPath,
    int RetentionCount,
    int ScheduleDayOfWeek,
    string ScheduleTime,
    string Timezone,
    string PgBinPath,
    DateTimeOffset? LastRunAt,
    bool UsesContainerPaths,
    string? HostBackupPath,
    string? HostBackupPathConfigKey,
    bool CanEditBackupPath,
    bool CanEditPgBinPath,
    bool OffsiteEnabled = false,
    string? Provider = null,
    string? GoogleDriveFolderId = null,
    int OffsiteRetentionCount = 0,
    bool UploadAfterBackup = false,
    BackupOffsiteConnectionStatus? OffsiteConnection = null);

public sealed record BackupSettingsUpdateRequest(
    bool Enabled,
    int ScheduleFrequency,
    string BackupPath,
    int RetentionCount,
    int ScheduleDayOfWeek,
    string ScheduleTime,
    string PgBinPath,
    bool OffsiteEnabled = false,
    string? Provider = null,
    string? GoogleDriveFolderId = null,
    int? OffsiteRetentionCount = null,
    bool UploadAfterBackup = false);

public sealed record BackupOffsiteConnectionStatus(
    bool IsConnected,
    string Provider,
    string? GoogleDriveFolderId,
    DateTimeOffset? ConnectedAt,
    DateTimeOffset? LastValidatedAt,
    string? LastError);

public sealed record BackupOffsiteConnectUrlRequest(
    string RedirectUri,
    string? GoogleDriveFolderId);

public sealed record BackupOffsiteConnectUrlResponse(string Url);

public sealed record BackupOffsiteGoogleDriveCallbackRequest(
    string Code,
    string RedirectUri,
    string? GoogleDriveFolderId);

public sealed record BackupOffsiteUploadDto(
    Guid Id,
    Guid? BackupJobId,
    string Provider,
    string Status,
    string? RemoteFileId,
    string? RemoteChecksum,
    long? RemoteFileSize,
    int AttemptCount,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? QueuedAt,
    DateTimeOffset? CompletedAt);

public sealed record BackupJobListItem(
    Guid Id,
    string Type,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? FileName,
    long? FileSize,
    string? ErrorMessage,
    Guid? CreatedBy);

public sealed record BackupJobDetail(
    Guid Id,
    string Type,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? FileName,
    long? FileSize,
    string? ErrorMessage,
    string? StdoutLog,
    string? StderrLog,
    DateTimeOffset? DownloadTokenExpiresAt,
    Guid? CreatedBy);

public sealed record BackupJobQuery(
    int Page,
    int PageSize,
    string? Status,
    string? Type);

public sealed record BackupAuditItem(
    Guid Id,
    string Action,
    Guid? ActorId,
    string Result,
    string? Details,
    DateTimeOffset CreatedAt);

public sealed record BackupUploadResult(
    Guid UploadId,
    string FileName,
    long FileSize,
    DateTimeOffset ExpiresAt);

public sealed record BackupRestoreRequest(
    Guid? JobId,
    Guid? UploadId,
    string ConfirmPhrase);