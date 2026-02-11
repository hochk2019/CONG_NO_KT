namespace CongNoGolden.Application.Reports;

public sealed record ReportChartsDto(
    IReadOnlyList<ReportChartPointDto> CashFlow,
    ReportAgingDistributionDto AgingDistribution,
    IReadOnlyList<ReportAllocationStatusDto> AllocationStatuses
);
