namespace CongNoGolden.Application.Reports;

public sealed record ReportTopCustomerDto(
    string CustomerTaxCode,
    string CustomerName,
    decimal Amount,
    int? DaysPastDue,
    decimal? Ratio
);
