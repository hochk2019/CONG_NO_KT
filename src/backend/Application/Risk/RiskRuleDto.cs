namespace CongNoGolden.Application.Risk;

public sealed record RiskRuleDto(
    string Level,
    int MinOverdueDays,
    decimal MinOverdueRatio,
    int MinLateCount,
    bool IsActive,
    string MatchMode);
