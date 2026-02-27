using CongNoGolden.Domain.Risk;

namespace CongNoGolden.Infrastructure.Services.RiskMl;

internal static class RiskMlFeatureEngineering
{
    internal const string ModelKey = "risk_overdue_30d";

    internal static readonly string[] FeatureNames =
    [
        "log_total_outstanding",
        "log_overdue_amount",
        "overdue_ratio",
        "max_days_past_due",
        "late_count",
        "month_sin",
        "month_cos",
        "weekday_sin",
        "weekday_cos"
    ];

    public static double[] BuildFeatureVector(RiskMetrics metrics, DateOnly asOfDate)
    {
        var monthAngle = 2d * Math.PI * ((asOfDate.Month - 1d) / 12d);
        var weekdayAngle = 2d * Math.PI * ((int)asOfDate.DayOfWeek / 7d);

        return
        [
            Log1p(metrics.TotalOutstanding),
            Log1p(metrics.OverdueAmount),
            Clamp((double)metrics.OverdueRatio, 0d, 1d),
            Math.Max(0d, metrics.MaxDaysPastDue),
            Math.Max(0d, metrics.LateCount),
            Math.Sin(monthAngle),
            Math.Cos(monthAngle),
            Math.Sin(weekdayAngle),
            Math.Cos(weekdayAngle)
        ];
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

    private static double Log1p(decimal value)
    {
        var nonNegative = Math.Max(0m, value);
        return Math.Log(1d + (double)nonNegative);
    }

    private static double Clamp(double value, double min, double max)
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
