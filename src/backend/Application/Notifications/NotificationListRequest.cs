namespace CongNoGolden.Application.Notifications;

public sealed record NotificationListRequest(
    bool? UnreadOnly,
    string? Source,
    string? Severity,
    string? Query,
    int Page,
    int PageSize);

public sealed record NotificationItem(
    Guid Id,
    string Title,
    string? Body,
    string Severity,
    string Source,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
