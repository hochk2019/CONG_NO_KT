namespace CongNoGolden.Application.Reports;

public static class ReportRequestValidator
{
    public static string? ValidateDateRange(DateOnly? from, DateOnly? to)
    {
        if (!from.HasValue || !to.HasValue)
        {
            return "Vui lòng chọn khoảng thời gian.";
        }

        if (from > to)
        {
            return "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.";
        }

        return null;
    }

    public static string? ValidateAsOfDate(DateOnly? asOfDate)
    {
        return asOfDate.HasValue ? null : "Vui lòng chọn \"Tính đến ngày\".";
    }
}
