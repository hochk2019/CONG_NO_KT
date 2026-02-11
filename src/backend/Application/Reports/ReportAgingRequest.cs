namespace CongNoGolden.Application.Reports;

public sealed record ReportAgingRequest(
    DateOnly? AsOfDate,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    Guid? OwnerId
);
