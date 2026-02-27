namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardKpiDeltaDto(
    decimal Current,
    decimal Previous,
    decimal Delta,
    decimal? DeltaPercent);
