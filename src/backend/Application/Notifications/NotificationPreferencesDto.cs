namespace CongNoGolden.Application.Notifications;

public sealed record NotificationPreferencesDto(
    bool ReceiveNotifications,
    bool PopupEnabled,
    IReadOnlyList<string> PopupSeverities,
    IReadOnlyList<string> PopupSources);

public sealed record NotificationPreferencesUpdate(
    bool ReceiveNotifications,
    bool PopupEnabled,
    IReadOnlyList<string> PopupSeverities,
    IReadOnlyList<string> PopupSources);

public sealed record NotificationUnreadCount(int Count);
