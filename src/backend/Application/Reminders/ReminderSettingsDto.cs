namespace CongNoGolden.Application.Reminders;

public sealed record ReminderSettingsDto(
    bool Enabled,
    int FrequencyDays,
    int UpcomingDueDays,
    IReadOnlyList<string> Channels,
    IReadOnlyList<string> TargetLevels,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? NextRunAt);

public sealed record ReminderSettingsUpdateRequest(
    bool Enabled,
    int FrequencyDays,
    int UpcomingDueDays,
    IReadOnlyList<string> Channels,
    IReadOnlyList<string> TargetLevels);
