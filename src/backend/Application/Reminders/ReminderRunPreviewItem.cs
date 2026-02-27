namespace CongNoGolden.Application.Reminders;

public sealed record ReminderRunPreviewItem(
    string CustomerTaxCode,
    string CustomerName,
    Guid? OwnerUserId,
    string? OwnerName,
    string Channel,
    string PlannedStatus,
    string? PlannedReason
);
