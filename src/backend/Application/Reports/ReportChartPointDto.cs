namespace CongNoGolden.Application.Reports;

public sealed record ReportChartPointDto(
    DateOnly Date,
    decimal Value
);
