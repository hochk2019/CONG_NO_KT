namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class ReportDeliverySchedule
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ReportKind { get; set; } = "FULL";
    public string ReportFormat { get; set; } = "XLSX";
    public string CronExpression { get; set; } = string.Empty;
    public string TimezoneId { get; set; } = "UTC";
    public string Recipients { get; set; } = "[]";
    public string FilterPayload { get; set; } = "{}";
    public bool Enabled { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReportDeliveryRun
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public string Status { get; set; } = "RUNNING";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? ErrorDetail { get; set; }
    public string? ArtifactMeta { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
