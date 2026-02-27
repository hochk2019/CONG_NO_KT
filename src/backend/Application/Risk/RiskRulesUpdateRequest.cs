namespace CongNoGolden.Application.Risk;

public sealed record RiskRuleUpdateItem(
    string Level,
    int MinOverdueDays,
    decimal MinOverdueRatio,
    int MinLateCount,
    bool IsActive,
    string? MatchMode = null);

public sealed record RiskRulesUpdateRequest(IReadOnlyList<RiskRuleUpdateItem> Rules);
