namespace CongNoGolden.Application.Reports;

public sealed record ReportSummaryPagedRequest(
    DateOnly? From,
    DateOnly? To,
    string? GroupBy,
    string? SellerTaxCode,
    string? CustomerTaxCode,
    Guid? OwnerId,
    int Page,
    int PageSize,
    string? SortKey,
    string? SortDirection
);
