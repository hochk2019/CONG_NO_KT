namespace CongNoGolden.Application.ReceiptHeldCredits;

public sealed record ReceiptHeldCreditListRequest(
    string? Status,
    string? Search,
    string? DocumentNo,
    string? ReceiptNo,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize);
