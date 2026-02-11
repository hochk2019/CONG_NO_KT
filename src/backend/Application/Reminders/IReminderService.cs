using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Reminders;

public interface IReminderService
{
    Task<ReminderSettingsDto> GetSettingsAsync(CancellationToken ct);
    Task UpdateSettingsAsync(ReminderSettingsUpdateRequest request, CancellationToken ct);
    Task<ReminderRunResult> RunAsync(bool force, CancellationToken ct);
    Task<PagedResult<ReminderLogItem>> ListLogsAsync(ReminderLogRequest request, CancellationToken ct);
}
