namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptListItem(
    Guid Id,
    string Status,
    int Version,
    string? ReceiptNo,
    DateOnly ReceiptDate,
    decimal Amount,
    decimal UnallocatedAmount,
    string AllocationMode,
    string AllocationStatus,
    string AllocationPriority,
    string? AllocationSource,
    DateTimeOffset? AllocationSuggestedAt,
    DateTimeOffset? LastReminderAt,
    DateTimeOffset? ReminderDisabledAt,
    string Method,
    string SellerTaxCode,
    string CustomerTaxCode,
    string? CustomerName,
    string? OwnerName,
    bool CanManage);
