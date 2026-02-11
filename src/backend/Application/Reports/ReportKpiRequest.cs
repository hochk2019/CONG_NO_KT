namespace CongNoGolden.Application.Reports;

public sealed record ReportKpiRequest(
    DateOnly? From,
    DateOnly? To,
    DateOnly? AsOfDate,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    Guid? OwnerId,
    int DueSoonDays
);
