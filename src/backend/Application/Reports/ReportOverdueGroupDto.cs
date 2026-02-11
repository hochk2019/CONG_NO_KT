namespace CongNoGolden.Application.Reports;

public sealed record ReportOverdueGroupDto(
    string GroupKey,
    string GroupName,
    decimal TotalOutstanding,
    decimal OverdueAmount,
    decimal OverdueRatio,
    int OverdueCustomers
);
