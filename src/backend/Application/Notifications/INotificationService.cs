using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Notifications;

public interface INotificationService
{
    Task<PagedResult<NotificationItem>> ListAsync(NotificationListRequest request, CancellationToken ct);
    Task MarkReadAsync(Guid id, CancellationToken ct);
    Task<NotificationUnreadCount> GetUnreadCountAsync(CancellationToken ct);
    Task MarkAllReadAsync(CancellationToken ct);
    Task<NotificationPreferencesDto> GetPreferencesAsync(CancellationToken ct);
    Task<NotificationPreferencesDto> UpdatePreferencesAsync(NotificationPreferencesUpdate request, CancellationToken ct);
}
