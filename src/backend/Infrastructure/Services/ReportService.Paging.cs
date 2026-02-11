namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    private static int NormalizePage(int page)
    {
        return page < 1 ? 1 : page;
    }

    private static int NormalizePageSize(int pageSize)
    {
        if (pageSize <= 0) return DefaultPageSize;
        return pageSize > MaxPageSize ? MaxPageSize : pageSize;
    }

    private static string NormalizeSortKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var span = value.AsSpan().Trim();
        var buffer = new char[span.Length];
        var length = 0;
        foreach (var ch in span)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[length++] = char.ToLowerInvariant(ch);
            }
        }
        return length == 0 ? string.Empty : new string(buffer, 0, length);
    }

    private static string NormalizeSortDirection(string? value)
    {
        return string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
    }
}
