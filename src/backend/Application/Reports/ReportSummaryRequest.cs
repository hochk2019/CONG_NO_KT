namespace CongNoGolden.Application.Reports;

public sealed record ReportSummaryRequest(
    DateOnly? From,
    DateOnly? To,
    string? GroupBy,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    Guid? OwnerId
);
