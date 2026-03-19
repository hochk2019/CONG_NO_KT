using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace CongNoGolden.Infrastructure.Services;

public static class ImportStagingHelpers
{
    public const string StatusOk = "OK";
    public const string StatusWarn = "WARN";
    public const string StatusError = "ERROR";

    public static bool IsRowEmpty(IXLRow row)
    {
        return row.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetString()));
    }

    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input
            .Trim()
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    public static DateOnly? ParseDate(IXLCell? cell)
    {
        if (cell is null)
        {
            return null;
        }

        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<DateTime>(out var dt))
        {
            return DateOnly.FromDateTime(dt);
        }

        var raw = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var formats = new[]
        {
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd-MM-yyyy",
            "d-M-yyyy"
        };
        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return DateOnly.FromDateTime(parsed);
        }

        if (DateTime.TryParse(raw, new CultureInfo("vi-VN"), DateTimeStyles.None, out parsed))
        {
            return DateOnly.FromDateTime(parsed);
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return DateOnly.FromDateTime(parsed);
        }

        return null;
    }

    public static decimal ParseDecimal(IXLCell? cell)
    {
        if (cell is null)
        {
            return 0m;
        }

        if (cell.IsEmpty())
        {
            return 0m;
        }

        if (cell.TryGetValue<double>(out var dbl))
        {
            return Convert.ToDecimal(dbl);
        }

        var raw = cell.GetString().Trim();
        if (decimal.TryParse(raw, NumberStyles.Any, new CultureInfo("vi-VN"), out var dec))
        {
            return dec;
        }

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out dec))
        {
            return dec;
        }

        return 0m;
    }

    public static void ValidateRequired(string value, string code, List<string> messages)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            messages.Add(code);
        }
    }

    public static string BuildKey(params string[] parts)
    {
        return string.Join("|", parts.Select(p => p?.Trim() ?? string.Empty));
    }

    public static string GetRootCustomerTaxCode(string? taxCode)
    {
        var trimmed = taxCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var separatorIndex = trimmed.IndexOf('-');
        return separatorIndex > 0 ? trimmed[..separatorIndex] : trimmed;
    }

    public static bool IsReductionAdjustmentNote(string? note)
    {
        var normalized = Normalize(note ?? string.Empty);
        return normalized.Contains("hoadondieuchinhgiam", StringComparison.Ordinal);
    }

    public static string? GetInformationalSkipCode(string? note, decimal revenue, decimal vat)
    {
        if (revenue != 0m || vat != 0m)
        {
            return null;
        }

        var normalized = Normalize(note ?? string.Empty);
        if (normalized.Contains("hoadonbithaythe", StringComparison.Ordinal))
        {
            return "INFO_REPLACED_INVOICE";
        }

        return null;
    }

    public static string GetStatus(List<string> messages)
    {
        if (messages.Any(m => m.EndsWith("REQUIRED", StringComparison.OrdinalIgnoreCase) || m == "NEGATIVE_AMOUNT"))
        {
            return StatusError;
        }

        return messages.Any() ? StatusWarn : StatusOk;
    }

    public static bool IsDataRow(string sttValue)
    {
        return int.TryParse(sttValue, out _);
    }
}
