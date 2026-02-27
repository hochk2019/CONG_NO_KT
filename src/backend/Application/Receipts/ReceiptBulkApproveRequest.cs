using System.Text.Json.Serialization;

namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptBulkApproveRequest(
    IReadOnlyList<ReceiptBulkApproveItem> Items,
    [property: JsonPropertyName("continue_on_error")] bool ContinueOnError = true
);

public sealed record ReceiptBulkApproveItem(
    [property: JsonPropertyName("receipt_id")] Guid ReceiptId,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("selected_targets")] IReadOnlyList<ReceiptTargetRef>? SelectedTargets = null,
    [property: JsonPropertyName("override_period_lock")] bool OverridePeriodLock = false,
    [property: JsonPropertyName("override_reason")] string? OverrideReason = null
);

public sealed record ReceiptBulkApproveItemResult(
    Guid ReceiptId,
    string Result,
    ReceiptPreviewResult? Preview,
    string? ErrorCode,
    string? ErrorMessage
);

public sealed record ReceiptBulkApproveResult(
    int Total,
    int Approved,
    int Failed,
    IReadOnlyList<ReceiptBulkApproveItemResult> Items
);
