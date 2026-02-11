using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptVoidRequest(
    string Reason,
    [property: JsonPropertyName("version")] int? Version,
    [property: JsonPropertyName("override_period_lock")] bool OverridePeriodLock = false,
    [property: JsonPropertyName("override_reason")] string? OverrideReason = null
);
