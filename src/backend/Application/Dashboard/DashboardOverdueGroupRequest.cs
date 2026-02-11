namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardOverdueGroupRequest(DateOnly? AsOf, int? Top, string? GroupBy);
