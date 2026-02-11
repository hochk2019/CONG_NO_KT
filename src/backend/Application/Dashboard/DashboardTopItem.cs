namespace CongNoGolden.Application.Dashboard;

public sealed record DashboardTopItem(
    string CustomerTaxCode,
    string CustomerName,
    decimal Amount,
    int? DaysPastDue);
