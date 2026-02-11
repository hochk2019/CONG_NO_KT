namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class BackupAudit
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
