namespace CongNoGolden.Infrastructure.Data.Entities;

public sealed class ImportBatch
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }
    public string? FileName { get; set; }
    public string? FileHash { get; set; }
    public Guid? IdempotencyKey { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SummaryData { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? CommittedAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
}

public sealed class ImportStagingRow
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public int RowNo { get; set; }
    public string RawData { get; set; } = string.Empty;
    public string ValidationStatus { get; set; } = string.Empty;
    public string? ValidationMessages { get; set; }
    public string? DedupKey { get; set; }
    public string? ActionSuggestion { get; set; }
    public Guid? MappedEntityId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
