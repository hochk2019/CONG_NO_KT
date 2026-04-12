namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class BackupSettings
{
    public Guid Id { get; set; }
    public bool Enabled { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public int RetentionCount { get; set; } = 10;
    public int ScheduleDayOfWeek { get; set; }
    public string ScheduleTime { get; set; } = "02:00";
    public string Timezone { get; set; } = "UTC";
    public string PgBinPath { get; set; } = string.Empty;
    public bool OffsiteEnabled { get; set; }
    public string? OffsiteProvider { get; set; }
    public string? GoogleDriveFolderId { get; set; }
    public int OffsiteRetentionCount { get; set; } = 10;
    public bool UploadAfterBackup { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
