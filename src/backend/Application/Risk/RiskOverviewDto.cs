namespace CongNoGolden.Application.Risk;

public sealed record RiskOverviewItem(
    string Level,
    int Customers,
    decimal TotalOutstanding,
    decimal OverdueAmount);

public sealed record RiskOverviewDto(
    DateOnly AsOfDate,
    IReadOnlyList<RiskOverviewItem> Items,
    int TotalCustomers,
    decimal TotalOutstanding,
    decimal TotalOverdue);
