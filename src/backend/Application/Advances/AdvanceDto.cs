namespace CongNoGolden.Application.Advances;

public sealed record AdvanceDto(
    Guid Id,
    string Status,
    int Version,
    decimal OutstandingAmount,
    string? AdvanceNo,
    DateOnly AdvanceDate,
    decimal Amount,
    string SellerTaxCode,
    string CustomerTaxCode
);
