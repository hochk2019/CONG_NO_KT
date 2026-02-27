namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardOverviewDto(
    DateOnly TrendFrom,
    DateOnly TrendTo,
    DashboardExecutiveSummaryDto ExecutiveSummary,
    DashboardKpiDto Kpis,
    DashboardKpiMoMDto KpiMoM,
    IReadOnlyList<DashboardTrendPoint> Trend,
    IReadOnlyList<DashboardCashflowForecastPoint> CashflowForecast,
    IReadOnlyList<DashboardTopItem> TopOutstanding,
    IReadOnlyList<DashboardTopItem> TopOnTime,
    IReadOnlyList<DashboardTopItem> TopOverdueDays,
    IReadOnlyList<DashboardAgingBucketDto> AgingBuckets,
    IReadOnlyList<DashboardAllocationStatusDto> AllocationStatuses,
    DateTime LastUpdatedAt);
