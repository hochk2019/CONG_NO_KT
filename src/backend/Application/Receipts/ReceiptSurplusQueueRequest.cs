namespace CongNoGolden.Application.Receipts;

public sealed record ReceiptSurplusQueueRequest(
    string? ItemType,
    string? Search,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    DateOnly? From,
    DateOnly? To,
    decimal? AmountMin,
    decimal? AmountMax,
    int Page,
    int PageSize);
