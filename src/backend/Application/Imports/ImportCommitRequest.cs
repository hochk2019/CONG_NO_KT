using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Imports;

public sealed record ImportCommitRequest(
    [property: JsonPropertyName("idempotency_key")] Guid? IdempotencyKey,
    [property: JsonPropertyName("override_period_lock")] bool OverridePeriodLock = false,
    [property: JsonPropertyName("override_reason")] string? OverrideReason = null
);
