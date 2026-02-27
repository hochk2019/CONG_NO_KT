namespace CongNoGolden.Application.Reminders;

public sealed record ReminderResponseStateDto(
    string CustomerTaxCode,
    string Channel,
    string ResponseStatus,
    DateTimeOffset? LatestResponseAt,
    bool EscalationLocked,
    int AttemptCount,
    int CurrentEscalationLevel,
    DateTimeOffset? LastSentAt,
    DateTimeOffset UpdatedAt);

public sealed record ReminderResponseStateUpsertRequest(
    string CustomerTaxCode,
    string Channel,
    string ResponseStatus,
    bool? EscalationLocked,
    DateTimeOffset? ResponseAt);
