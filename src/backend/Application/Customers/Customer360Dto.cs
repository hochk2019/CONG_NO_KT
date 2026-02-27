namespace CongNoGolden.Application.Customers;

public sealed record Customer360Dto(
    string TaxCode,
    string Name,
    string Status,
    decimal CurrentBalance,
    int PaymentTermsDays,
    decimal? CreditLimit,
    string? OwnerName,
    string? ManagerName,
    Customer360SummaryDto Summary,
    Customer360RiskSnapshotDto RiskSnapshot,
    IReadOnlyList<Customer360ReminderLogDto> ReminderTimeline,
    IReadOnlyList<Customer360ResponseStateDto> ResponseStates);

public sealed record Customer360SummaryDto(
    decimal TotalOutstanding,
    decimal OverdueAmount,
    decimal OverdueRatio,
    int MaxDaysPastDue,
    int OpenInvoiceCount,
    DateOnly? NextDueDate);

public sealed record Customer360RiskSnapshotDto(
    decimal? Score,
    string? Signal,
    DateOnly? AsOfDate,
    string? ModelVersion,
    DateTimeOffset? CreatedAt);

public sealed record Customer360ReminderLogDto(
    Guid Id,
    string Channel,
    string Status,
    string RiskLevel,
    int EscalationLevel,
    string? EscalationReason,
    string? Message,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt);

public sealed record Customer360ResponseStateDto(
    string Channel,
    string ResponseStatus,
    DateTimeOffset? LatestResponseAt,
    bool EscalationLocked,
    int AttemptCount,
    int CurrentEscalationLevel,
    DateTimeOffset? LastSentAt,
    DateTimeOffset UpdatedAt);
