namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardExecutiveSummaryDto(
    string Status,
    string Message,
    string ActionHint,
    DateTime GeneratedAt);
