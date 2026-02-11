namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardOverviewDto(
    DateOnly TrendFrom,
    DateOnly TrendTo,
    DashboardKpiDto Kpis,
    IReadOnlyList<DashboardTrendPoint> Trend,
    IReadOnlyList<DashboardTopItem> TopOutstanding,
    IReadOnlyList<DashboardTopItem> TopOnTime,
    IReadOnlyList<DashboardTopItem> TopOverdueDays,
    IReadOnlyList<DashboardAgingBucketDto> AgingBuckets,
    IReadOnlyList<DashboardAllocationStatusDto> AllocationStatuses,
    DateTime LastUpdatedAt);
