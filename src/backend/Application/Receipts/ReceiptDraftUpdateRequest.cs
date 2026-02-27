using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptDraftUpdateRequest(
    string? ReceiptNo,
    DateOnly ReceiptDate,
    decimal Amount,
    string AllocationMode,
    DateOnly? AppliedPeriodStart,
    string? Method,
    string? Description,
    string? AllocationPriority,
    IReadOnlyList<ReceiptTargetRef>? SelectedTargets,
    [property: JsonPropertyName("version")] int? Version
);
