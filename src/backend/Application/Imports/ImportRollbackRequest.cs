using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Imports;

public sealed record ImportRollbackRequest(
    [property: JsonPropertyName("override_period_lock")] bool OverridePeriodLock = false,
    [property: JsonPropertyName("override_reason")] string? OverrideReason = null
);
