namespace CongNoGolden.Domain.Risk;

public sealed record RiskRule(
    RiskLevel Level,
    int MinOverdueDays,
    decimal MinOverdueRatio,
    int MinLateCount,
    bool IsActive = true);

public sealed record RiskMetrics(
    decimal TotalOutstanding,
    decimal OverdueAmount,
    decimal OverdueRatio,
    int MaxDaysPastDue,
    int LateCount);
