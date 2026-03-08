namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptSurplusQueueItem(
    Guid Id,
    string ItemType,
    int Version,
    string Status,
    Guid ReceiptId,
    string? ReceiptNo,
    DateOnly ReceiptDate,
    string SellerTaxCode,
    string CustomerTaxCode,
    string? CustomerName,
    string? OwnerName,
    string? OriginalInvoiceNo,
    DateOnly? OriginalInvoiceDate,
    decimal AmountRemaining,
    int AgeDays,
    bool CanManage);
