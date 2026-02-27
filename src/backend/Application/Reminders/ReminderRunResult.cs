namespace CongNoGolden.Application.Reminders;

public sealed record ReminderRunResult(
    DateTimeOffset RunAt,
    int TotalCandidates,
    int SentCount,
    int FailedCount,
    int SkippedCount,
    bool DryRun = false,
    IReadOnlyList<ReminderRunPreviewItem>? PreviewItems = null);
