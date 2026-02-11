namespace CongNoGolden.Application.Reports;

public sealed record ReportStatementRequest(
    string? CustomerTaxCode,
    DateOnly? From,
    DateOnly? To,
    string? SellerTaxCode
);
