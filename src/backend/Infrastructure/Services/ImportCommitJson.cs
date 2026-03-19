using System.Globalization;
using System.Text.Json;

namespace CongNoGolden.Infrastructure.Services;

public static class ImportCommitJson
{
    public static string GetString(JsonElement raw, string property)
    {
        if (raw.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    public static decimal GetDecimal(JsonElement raw, string property)
    {
        if (raw.TryGetProperty(property, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var dec))
            {
                return dec;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var rawString = value.GetString() ?? string.Empty;
                if (decimal.TryParse(rawString, NumberStyles.Any, CultureInfo.InvariantCulture, out dec))
                {
                    return dec;
                }
            }
        }
        return 0m;
    }

    public static DateOnly? GetDate(JsonElement raw, string property)
    {
        if (raw.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
        {
            var rawString = value.GetString();
            var formats = new[]
            {
                "yyyy-MM-dd",
                "dd/MM/yyyy",
                "d/M/yyyy",
                "dd-MM-yyyy",
                "d-M-yyyy"
            };
            if (DateOnly.TryParseExact(rawString, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
            if (DateOnly.TryParse(rawString, new CultureInfo("vi-VN"), DateTimeStyles.None, out date))
            {
                return date;
            }
            if (DateOnly.TryParse(rawString, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return date;
            }
        }
        return null;
    }

    public static string GetInvoiceType(JsonElement raw)
    {
        var invoiceType = GetString(raw, "invoice_type").Trim();
        return string.IsNullOrWhiteSpace(invoiceType) ? "NORMAL" : invoiceType;
    }

    public static bool IsReductionInvoice(JsonElement raw)
    {
        return string.Equals(GetInvoiceType(raw), "ADJUSTMENT_REDUCTION", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveInvoiceCustomerTaxCode(JsonElement raw)
    {
        if (IsReductionInvoice(raw))
        {
            var matchingTaxCode = GetString(raw, "customer_tax_code_matching").Trim();
            if (!string.IsNullOrWhiteSpace(matchingTaxCode))
            {
                return matchingTaxCode;
            }
        }

        return GetString(raw, "customer_tax_code").Trim();
    }
}
