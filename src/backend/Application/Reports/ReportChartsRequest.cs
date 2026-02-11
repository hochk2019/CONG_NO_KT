namespace CongNoGolden.Application.Reports;

public sealed record ReportChartsRequest(
    DateOnly? From,
    DateOnly? To,
    DateOnly? AsOfDate,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    Guid? OwnerId
);
