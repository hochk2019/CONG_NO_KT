using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptAutoAllocateUpdateRequest(
    bool AutoAllocateEnabled,
    [property: JsonPropertyName("version")] int? Version
);
