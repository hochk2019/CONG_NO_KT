using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Reminders;

public sealed record ReminderRunRequest(
    [property: JsonPropertyName("force")] bool Force = true,
    [property: JsonPropertyName("dry_run")] bool DryRun = false,
    [property: JsonPropertyName("preview_limit")] int PreviewLimit = 50
);
