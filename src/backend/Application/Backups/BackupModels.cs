namespace CongNoGolden.Application.Backups;

public sealed record BackupSettingsDto(
    bool Enabled,
    string BackupPath,
    int RetentionCount,
    int ScheduleDayOfWeek,
    string ScheduleTime,
    string Timezone,
    string PgBinPath,
    DateTimeOffset? LastRunAt);

public sealed record BackupSettingsUpdateRequest(
    bool Enabled,
    string BackupPath,
    int RetentionCount,
    int ScheduleDayOfWeek,
    string ScheduleTime,
    string PgBinPath);

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
