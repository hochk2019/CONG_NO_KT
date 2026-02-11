namespace CongNoGolden.Application.Advances;

public sealed record AdvanceListRequest(
    string? SellerTaxCode,
    string? CustomerTaxCode,
    string? Status,
    string? AdvanceNo,
    DateOnly? From,
    DateOnly? To,
    decimal? AmountMin,
    decimal? AmountMax,
    string? Source,
    int Page,
    int PageSize);
