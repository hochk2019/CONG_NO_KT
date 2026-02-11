namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardOverdueGroupItem(
    string GroupKey,
    string GroupName,
    decimal TotalOutstanding,
    decimal OverdueAmount,
    decimal OverdueRatio,
    int OverdueCustomers);
