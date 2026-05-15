namespace CongNoGolden.Application.Dashboard;

public sealed record Dashboard30DayMetricsDto(
    decimal Actual30Days,
    decimal Expected30DaysPast,
    decimal Expected30DaysNext,
    int OnTimeCustomers30Days);
