namespace CongNoGolden.Application.Reports;

public sealed record ReportAgingPagedRequest(
    DateOnly? AsOfDate,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    Guid? OwnerId,
    int Page,
    int PageSize,
    string? SortKey,
    string? SortDirection
);
