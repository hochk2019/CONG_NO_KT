namespace CongNoGolden.Domain.Risk;

public enum RiskMatchMode
{
    Any = 0,
    All = 1
}

public static class RiskMatchModeExtensions
{
    public static string ToCode(this RiskMatchMode mode)
    {
        return mode switch
        {
            RiskMatchMode.All => "ALL",
            _ => "ANY"
        };
    }

    public static bool TryParse(string? value, out RiskMatchMode mode)
    {
        mode = RiskMatchMode.Any;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            mode = RiskMatchMode.All;
            return true;
        }

        if (value.Equals("ANY", StringComparison.OrdinalIgnoreCase))
        {
            mode = RiskMatchMode.Any;
            return true;
        }

        return false;
    }
}
