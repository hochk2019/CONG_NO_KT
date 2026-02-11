namespace CongNoGolden.Application.Reports;

public sealed record ReportStatementPagedRequest(
    DateOnly? From,
    DateOnly? To,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    int Page,
    int PageSize
);
