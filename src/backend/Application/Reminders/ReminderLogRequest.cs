namespace CongNoGolden.Application.Reminders;

public sealed record ReminderLogRequest(
    string? Channel,
    string? Status,
    Guid? OwnerId,
    int Page,
    int PageSize);

public sealed record ReminderLogItem(
    Guid Id,
    string CustomerTaxCode,
    string CustomerName,
    Guid? OwnerUserId,
    string? OwnerName,
    string RiskLevel,
    string Channel,
    string Status,
    string? Message,
    string? ErrorDetail,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt);
