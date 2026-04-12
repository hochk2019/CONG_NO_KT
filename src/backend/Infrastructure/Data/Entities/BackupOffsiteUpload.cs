namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class BackupOffsiteUpload
{
    public Guid Id { get; set; }
    public Guid? BackupJobId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RemoteFileId { get; set; }
    public string? RemoteChecksum { get; set; }
    public long? RemoteFileSize { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? QueuedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
