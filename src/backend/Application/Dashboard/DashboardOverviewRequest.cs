namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardOverviewRequest(
    DateOnly? From,
    DateOnly? To,
    int? Months,
    int? Top,
    string? TrendGranularity,
    int? TrendPeriods);
