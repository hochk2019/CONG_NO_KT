namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardCashflowForecastPoint(
    string Period,
    decimal ExpectedTotal,
    decimal ActualTotal,
    decimal Variance);
