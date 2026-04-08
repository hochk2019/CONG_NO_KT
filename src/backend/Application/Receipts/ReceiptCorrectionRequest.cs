using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptCorrectionRequest(
    string ReceiptNo,
    DateOnly ReceiptDate,
    decimal Amount,
    string AllocationMode,
    DateOnly? AppliedPeriodStart,
    string? Method,
    string? Description,
    string? AllocationPriority,
    IReadOnlyList<ReceiptTargetRef>? SelectedTargets,
    string? Reason,
    [property: JsonPropertyName("version")] int? Version
);
