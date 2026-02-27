namespace CongNoGolden.Domain.Risk;

public static class RiskClassifier
{
    public static RiskLevel Classify(RiskMetrics metrics, IEnumerable<RiskRule> rules)
    {
        var ordered = rules
            .Where(rule => rule.IsActive)
            .OrderByDescending(rule => (int)rule.Level)
            .ToList();

        foreach (var rule in ordered)
        {
            if (Matches(metrics, rule))
            {
                return rule.Level;
            }
        }

        return RiskLevel.Low;
    }

    private static bool Matches(RiskMetrics metrics, RiskRule rule)
    {
        return rule.MatchMode switch
        {
            RiskMatchMode.All =>
                metrics.MaxDaysPastDue >= rule.MinOverdueDays
                && metrics.OverdueRatio >= rule.MinOverdueRatio
                && metrics.LateCount >= rule.MinLateCount,
            _ =>
                metrics.MaxDaysPastDue >= rule.MinOverdueDays
                || metrics.OverdueRatio >= rule.MinOverdueRatio
                || metrics.LateCount >= rule.MinLateCount
        };
    }
}
