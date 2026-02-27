using CongNoGolden.Application.Common;

namespace CongNoGolden.Application.Reminders;

public interface IReminderService
{
    Task<ReminderSettingsDto> GetSettingsAsync(CancellationToken ct);
    Task UpdateSettingsAsync(ReminderSettingsUpdateRequest request, CancellationToken ct);
    Task<ReminderRunResult> RunAsync(ReminderRunRequest request, CancellationToken ct);
    Task<PagedResult<ReminderLogItem>> ListLogsAsync(ReminderLogRequest request, CancellationToken ct);
    Task<ReminderResponseStateDto?> GetResponseStateAsync(string customerTaxCode, string channel, CancellationToken ct);
    Task<ReminderResponseStateDto> UpsertResponseStateAsync(ReminderResponseStateUpsertRequest request, CancellationToken ct);
}
