namespace CongNoGolden.Application.Advances;

public sealed record AdvanceCreateRequest(
    string SellerTaxCode,
    string CustomerTaxCode,
    string? AdvanceNo,
    DateOnly AdvanceDate,
    decimal Amount,
    string? Description
);
