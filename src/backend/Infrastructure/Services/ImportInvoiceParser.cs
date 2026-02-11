using System.Text.Json;
using ClosedXML.Excel;
using CongNoGolden.Infrastructure.Data.Entities;

namespace CongNoGolden.Infrastructure.Services;

public static class ImportInvoiceParser
{
    public static List<ImportStagingRow> ParseReportDetail(IXLWorksheet sheet, Guid batchId)
    {
        var headerRow = sheet.RowsUsed().FirstOrDefault(r =>
            r.CellsUsed().Any(c => ImportStagingHelpers.Normalize(c.GetString()) == "stt") &&
            r.CellsUsed().Any(c => ImportStagingHelpers.Normalize(c.GetString()).Contains("buyername")));

        if (headerRow is null)
        {
            return new List<ImportStagingRow>();
        }

        var sttCol = FindColumn(headerRow, "stt");
        var buyerNameCol = FindColumnContains(headerRow, "tennguoimua", "buyername");
        var buyerTaxCol = FindColumnContains(headerRow, "masothenguoi", "taxcode");
        var revenueCol = FindColumnContains(headerRow, "doanhsobanchuacothue", "revenueexcludingvat");
        var vatCol = FindColumnContains(headerRow, "thuegtgt", "vatamount");
        var noteCol = FindColumnContains(headerRow, "ghichu", "note");

        var invoiceHeader = sheet.RowsUsed().FirstOrDefault(r =>
            r.CellsUsed().Any(c => ImportStagingHelpers.Normalize(c.GetString()).Contains("kyhieumau")) &&
            r.CellsUsed().Any(c => ImportStagingHelpers.Normalize(c.GetString()).Contains("sohoadon")));

        var templateCol = invoiceHeader is null ? 3 : FindColumnContains(invoiceHeader, "kyhieumau");
        var seriesCol = invoiceHeader is null ? 4 : FindColumnContains(invoiceHeader, "sohieuhoadon");
        var invoiceNoCol = invoiceHeader is null ? 5 : FindColumnContains(invoiceHeader, "sohoadon");
        var issueDateCol = invoiceHeader is null ? 6 : FindColumnContains(invoiceHeader, "ngaythangnamphathanh");

        var sellerTaxCode = FindSellerTaxCode(sheet);
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ImportStagingRow>();

        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var sttValue = row.Cell(sttCol).GetString().Trim();
            if (!ImportStagingHelpers.IsDataRow(sttValue))
            {
                continue;
            }

            var buyerName = row.Cell(buyerNameCol).GetString().Trim();
            var buyerTax = row.Cell(buyerTaxCol).GetString().Trim();
            var templateCode = row.Cell(templateCol).GetString().Trim();
            var series = row.Cell(seriesCol).GetString().Trim();
            var invoiceNo = row.Cell(invoiceNoCol).GetString().Trim();
            var issueDate = ImportStagingHelpers.ParseDate(row.Cell(issueDateCol));
            var revenue = ImportStagingHelpers.ParseDecimal(row.Cell(revenueCol));
            var vat = ImportStagingHelpers.ParseDecimal(row.Cell(vatCol));
            var note = noteCol > 0 ? row.Cell(noteCol).GetString().Trim() : null;

            var messages = new List<string>();
            ImportStagingHelpers.ValidateRequired(buyerTax, "BUYER_TAX_REQUIRED", messages);
            ImportStagingHelpers.ValidateRequired(buyerName, "BUYER_NAME_REQUIRED", messages);
            ImportStagingHelpers.ValidateRequired(invoiceNo, "INVOICE_NO_REQUIRED", messages);
            if (issueDate is null)
            {
                messages.Add("ISSUE_DATE_REQUIRED");
            }
            if (sellerTaxCode.Length == 0)
            {
                messages.Add("SELLER_TAX_REQUIRED");
            }
            if (revenue < 0 || vat < 0)
            {
                messages.Add("NEGATIVE_AMOUNT");
            }

            var total = revenue + vat;
            var dedupKey = ImportStagingHelpers.BuildKey(sellerTaxCode, buyerTax, series, invoiceNo, issueDate?.ToString() ?? string.Empty);
            var isDup = !dedup.Add(dedupKey);
            if (isDup)
            {
                messages.Add("DUP_IN_FILE");
            }

            var status = ImportStagingHelpers.GetStatus(messages);
            var action = status == ImportStagingHelpers.StatusError || isDup ? "SKIP" : "INSERT";

            var raw = new Dictionary<string, object?>
            {
                ["seller_tax_code"] = sellerTaxCode,
                ["customer_tax_code"] = buyerTax,
                ["customer_name"] = buyerName,
                ["invoice_template_code"] = templateCode,
                ["invoice_series"] = series,
                ["invoice_no"] = invoiceNo,
                ["issue_date"] = issueDate?.ToString("yyyy-MM-dd"),
                ["revenue_excl_vat"] = revenue,
                ["vat_amount"] = vat,
                ["total_amount"] = total,
                ["note"] = note
            };

            results.Add(new ImportStagingRow
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                RowNo = row.RowNumber(),
                RawData = JsonSerializer.Serialize(raw),
                ValidationStatus = status,
                ValidationMessages = JsonSerializer.Serialize(messages),
                DedupKey = dedupKey,
                ActionSuggestion = action,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        return results;
    }

    private static int FindColumn(IXLRow row, string token)
    {
        foreach (var cell in row.CellsUsed())
        {
            if (ImportStagingHelpers.Normalize(cell.GetString()) == token)
            {
                return cell.Address.ColumnNumber;
            }
        }
        return 1;
    }

    private static int FindColumnContains(IXLRow row, params string[] tokens)
    {
        foreach (var cell in row.CellsUsed())
        {
            var norm = ImportStagingHelpers.Normalize(cell.GetString());
            foreach (var token in tokens)
            {
                if (norm.Contains(token))
                {
                    return cell.Address.ColumnNumber;
                }
            }
        }
        return 1;
    }

    private static string FindSellerTaxCode(IXLWorksheet sheet)
    {
        foreach (var row in sheet.RowsUsed().Take(10))
        {
            foreach (var cell in row.CellsUsed())
            {
                var norm = ImportStagingHelpers.Normalize(cell.GetString());
                if (norm.Contains("masothue"))
                {
                    var value = row.CellsUsed()
                        .Where(c => c.Address.ColumnNumber > cell.Address.ColumnNumber)
                        .Select(c => c.GetString().Trim())
                        .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
        }

        return string.Empty;
    }
}
