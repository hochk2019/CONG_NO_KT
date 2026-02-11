namespace CongNoGolden.Application.Reports;

public sealed record ReportInsightsDto(
    IReadOnlyList<ReportTopCustomerDto> TopOutstanding,
    IReadOnlyList<ReportTopCustomerDto> TopOnTime,
    IReadOnlyList<ReportOverdueGroupDto> OverdueByOwner
);
