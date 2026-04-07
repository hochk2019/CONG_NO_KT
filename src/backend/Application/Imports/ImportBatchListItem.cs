namespace CongNoGolden.Application.Imports;

public sealed record ImportBatchListItem(
    Guid BatchId,
    string Type,
    string Status,
    string? FileName,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? CommittedAt,
    ImportCommitResult Summary,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    string? CancelReason,
    ImportBatchStagingSummary? StagingSummary);

public sealed record ImportBatchStagingSummary(
    int TotalRows,
    int OkCount,
    int WarnCount,
    int ErrorCount);
