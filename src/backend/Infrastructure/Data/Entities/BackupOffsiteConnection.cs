namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class BackupOffsiteConnection
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? EncryptedRefreshToken { get; set; }
    public string? GoogleDriveFolderId { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset? LastValidatedAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
