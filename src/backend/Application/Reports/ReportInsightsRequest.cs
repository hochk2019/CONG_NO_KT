namespace CongNoGolden.Application.Reports;

public sealed record ReportInsightsRequest(
    DateOnly? From,
    DateOnly? To,
    DateOnly? AsOfDate,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    Guid? OwnerId,
    int Top
);
