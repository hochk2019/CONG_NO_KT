namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptListRequest(
    string? SellerTaxCode,
    string? CustomerTaxCode,
    string? Status,
    string? AllocationStatus,
    string? DocumentNo,
    DateOnly? From,
    DateOnly? To,
    decimal? AmountMin,
    decimal? AmountMax,
    string? Method,
    string? AllocationPriority,
    bool? ReminderEnabled,
    int Page,
    int PageSize);
