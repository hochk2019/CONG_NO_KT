namespace CongNoGolden.Domain.Risk;

public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    VeryHigh = 4
}

public static class RiskLevelExtensions
{
    public static string ToCode(this RiskLevel level) => level switch
    {
        RiskLevel.VeryHigh => "VERY_HIGH",
        RiskLevel.High => "HIGH",
        RiskLevel.Medium => "MEDIUM",
        RiskLevel.Low => "LOW",
        _ => "LOW"
    };

    public static bool TryParse(string? code, out RiskLevel level)
    {
        switch ((code ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "VERY_HIGH":
                level = RiskLevel.VeryHigh;
                return true;
            case "HIGH":
                level = RiskLevel.High;
                return true;
            case "MEDIUM":
                level = RiskLevel.Medium;
                return true;
            case "LOW":
                level = RiskLevel.Low;
                return true;
            default:
                level = RiskLevel.Low;
                return false;
        }
    }
}
