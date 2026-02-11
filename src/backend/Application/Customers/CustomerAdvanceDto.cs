namespace CongNoGolden.Application.Customers;

public sealed record CustomerAdvanceDto(
    Guid Id,
    string? AdvanceNo,
    DateOnly AdvanceDate,
    decimal Amount,
    decimal OutstandingAmount,
    string Status,
    int Version,
    string SellerTaxCode,
    string? SellerShortName,
    IReadOnlyList<CustomerReceiptRefDto> ReceiptRefs);
