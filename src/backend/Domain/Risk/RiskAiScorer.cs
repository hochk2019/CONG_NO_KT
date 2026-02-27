namespace CongNoGolden.Domain.Risk;

public sealed record RiskAiFactorContribution(
    string Code,
    string Label,
    decimal RawValue,
    decimal NormalizedValue,
    decimal Weight,
    decimal Contribution);

public sealed record RiskAiPrediction(
    decimal Probability,
    string Signal,
    IReadOnlyList<RiskAiFactorContribution> Factors,
    string Recommendation);

public static class RiskAiScorer
{
    private const decimal MaxDaysWindow = 90m;
    private const decimal MaxLateCountWindow = 8m;
    private const decimal MaxOutstandingWindow = 500_000_000m;

    public static RiskAiPrediction Predict(RiskMetrics metrics)
    {
        var factors = BuildFactors(metrics);
        var weightedScore = factors.Sum(factor => factor.Contribution);
        var probability = ToProbability(weightedScore);
        var rounded = Math.Round(probability, 4, MidpointRounding.AwayFromZero);
        var signal = ResolveSignal(rounded);

        return new RiskAiPrediction(
            rounded,
            signal,
            factors,
            ResolveRecommendation(signal));
    }

    public static IReadOnlyList<RiskAiFactorContribution> BuildFactors(RiskMetrics metrics)
    {
        var overdueRatio = Clamp(metrics.OverdueRatio, 0m, 1m);
        var normalizedDays = NormalizeWindow(metrics.MaxDaysPastDue, MaxDaysWindow);
        var normalizedLate = NormalizeWindow(metrics.LateCount, MaxLateCountWindow);
        var normalizedOutstanding = NormalizeWindow(metrics.TotalOutstanding, MaxOutstandingWindow);

        return
        [
            CreateFactor(
                "OVERDUE_RATIO",
                "Tỷ lệ quá hạn",
                metrics.OverdueRatio,
                overdueRatio,
                0.48m),
            CreateFactor(
                "MAX_DAYS_PAST_DUE",
                "Số ngày quá hạn lớn nhất",
                metrics.MaxDaysPastDue,
                normalizedDays,
                0.27m),
            CreateFactor(
                "LATE_COUNT",
                "Số khoản trễ hạn",
                metrics.LateCount,
                normalizedLate,
                0.15m),
            CreateFactor(
                "TOTAL_OUTSTANDING",
                "Tổng dư nợ",
                metrics.TotalOutstanding,
                normalizedOutstanding,
                0.10m)
        ];
    }

    public static string ResolveRecommendation(string signal)
    {
        return signal.Trim().ToUpperInvariant() switch
        {
            "CRITICAL" => "Khoá cấp tín dụng mới, liên hệ khách hàng trong 24h và chốt lịch thu hồi có cam kết.",
            "HIGH" => "Liên hệ xác nhận kế hoạch thanh toán trong 48h, cân nhắc giảm hạn mức tạm thời.",
            "MEDIUM" => "Theo dõi hàng tuần, gửi nhắc tự động và rà soát chứng từ đối soát.",
            _ => "Duy trì lịch nhắc định kỳ và theo dõi xu hướng thanh toán trong kỳ tới."
        };
    }

    private static RiskAiFactorContribution CreateFactor(
        string code,
        string label,
        decimal rawValue,
        decimal normalizedValue,
        decimal weight)
    {
        return new RiskAiFactorContribution(
            code,
            label,
            rawValue,
            normalizedValue,
            weight,
            Math.Round(normalizedValue * weight, 4, MidpointRounding.AwayFromZero));
    }

    private static decimal ToProbability(decimal weightedScore)
    {
        var logit = (double)((weightedScore * 4m) - 2m);
        var probability = 1d / (1d + Math.Exp(-logit));
        return Clamp((decimal)probability, 0m, 1m);
    }

    public static string ResolveSignal(decimal probability)
    {
        if (probability >= 0.80m)
        {
            return "CRITICAL";
        }

        if (probability >= 0.60m)
        {
            return "HIGH";
        }

        if (probability >= 0.35m)
        {
            return "MEDIUM";
        }

        return "LOW";
    }

    private static decimal NormalizeWindow(decimal value, decimal maxWindow)
    {
        if (maxWindow <= 0)
        {
            return 0m;
        }

        return Clamp(value, 0m, maxWindow) / maxWindow;
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
