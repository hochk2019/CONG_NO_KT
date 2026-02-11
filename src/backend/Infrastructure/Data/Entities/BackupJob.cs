namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class BackupJob
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? FilePath { get; set; }
    public Guid? CreatedBy { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StdoutLog { get; set; }
    public string? StderrLog { get; set; }
    public string? DownloadToken { get; set; }
    public DateTimeOffset? DownloadTokenExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
