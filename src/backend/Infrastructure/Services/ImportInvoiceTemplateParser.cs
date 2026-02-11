using System.Text.Json;
using ClosedXML.Excel;
using CongNoGolden.Infrastructure.Data.Entities;

namespace CongNoGolden.Infrastructure.Services;

public static class ImportInvoiceTemplateParser
{
    public static List<ImportStagingRow> ParseSimpleTemplate(IXLWorksheet sheet, Guid batchId)
    {
        var header = sheet.FirstRowUsed();
        if (header is null)
        {
            return new List<ImportStagingRow>();
        }

        var map = BuildColumnMap(header);
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ImportStagingRow>();

        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
        {
            if (ImportStagingHelpers.IsRowEmpty(row))
            {
                continue;
            }

            var seller = GetCell(row, map, "seller_tax_code");
            var customer = GetCell(row, map, "customer_tax_code");
            var customerName = GetCell(row, map, "customer_name");
            var templateCode = GetCell(row, map, "invoice_template_code");
            var series = GetCell(row, map, "invoice_series");
            var invoiceNo = GetCell(row, map, "invoice_no");
            var issueDate = ImportStagingHelpers.ParseDate(GetCellCell(row, map, "issue_date"));
            var revenue = ImportStagingHelpers.ParseDecimal(GetCellCell(row, map, "revenue_excl_vat"));
            var vat = ImportStagingHelpers.ParseDecimal(GetCellCell(row, map, "vat_amount"));
            var total = ImportStagingHelpers.ParseDecimal(GetCellCell(row, map, "total_amount"));
            var note = GetCell(row, map, "note");

            if (total <= 0)
            {
                total = revenue + vat;
            }

            var messages = new List<string>();
            ImportStagingHelpers.ValidateRequired(seller, "SELLER_TAX_REQUIRED", messages);
            ImportStagingHelpers.ValidateRequired(customer, "BUYER_TAX_REQUIRED", messages);
            ImportStagingHelpers.ValidateRequired(invoiceNo, "INVOICE_NO_REQUIRED", messages);
            if (issueDate is null)
            {
                messages.Add("ISSUE_DATE_REQUIRED");
            }
            if (revenue < 0 || vat < 0 || total < 0)
            {
                messages.Add("NEGATIVE_AMOUNT");
            }

            var raw = new Dictionary<string, object?>
            {
                ["seller_tax_code"] = seller,
                ["customer_tax_code"] = customer,
                ["customer_name"] = customerName,
                ["invoice_template_code"] = templateCode,
                ["invoice_series"] = series,
                ["invoice_no"] = invoiceNo,
                ["issue_date"] = issueDate?.ToString("yyyy-MM-dd"),
                ["revenue_excl_vat"] = revenue,
                ["vat_amount"] = vat,
                ["total_amount"] = total,
                ["note"] = note
            };

            var dedupKey = ImportStagingHelpers.BuildKey(
                seller,
                customer,
                series,
                invoiceNo,
                issueDate?.ToString() ?? string.Empty);
            var isDup = !dedup.Add(dedupKey);
            if (isDup)
            {
                messages.Add("DUP_IN_FILE");
            }

            var status = ImportStagingHelpers.GetStatus(messages);
            var action = status == ImportStagingHelpers.StatusError || isDup ? "SKIP" : "INSERT";

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

    private static string GetCell(IXLRow row, Dictionary<string, int> map, string key)
    {
        return map.TryGetValue(key, out var col) ? row.Cell(col).GetString().Trim() : string.Empty;
    }

    private static IXLCell? GetCellCell(IXLRow row, Dictionary<string, int> map, string key)
    {
        return map.TryGetValue(key, out var col) ? row.Cell(col) : null;
    }

    private static Dictionary<string, int> BuildColumnMap(IXLRow header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in header.CellsUsed())
        {
            var norm = ImportStagingHelpers.Normalize(cell.GetString());
            if (string.IsNullOrWhiteSpace(norm))
            {
                continue;
            }

            SetIfMatch(map, norm, cell.Address.ColumnNumber, "seller_tax_code", new[]
            {
                "seller_tax_code", "sellertaxcode", "mstban", "masothueban", "sellertax"
            });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "customer_tax_code", new[]
            {
                "customer_tax_code", "customertaxcode", "mstmua", "masothuemua", "customertax"
            });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "customer_name", new[]
            {
                "customer_name", "buyername", "tennguoimua", "tenkhachhang"
            });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "invoice_template_code", new[]
            {
                "invoice_template_code", "kyhieumau", "mauhoadon", "templateno"
            });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "invoice_series", new[]
            {
                "invoice_series", "sohieuhoadon", "series"
            });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "invoice_no", new[]
            {
                "invoice_no", "sohoadon", "invoiceno"
            });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "issue_date", new[]
            {
                "issue_date", "ngayphathanh", "ngayhoadon", "ngaythangnamphathanh"
            });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "revenue_excl_vat", new[]
            {
                "revenue_excl_vat", "revenueexcludingvat", "doanhsobanchuacothue", "doanhsochuacothue"
            });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "vat_amount", new[]
            {
                "vat_amount", "vatamount", "thuegtgt", "tienvat"
            });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "total_amount", new[]
            {
                "total_amount", "total", "tongtien", "tongcong"
            });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "note", new[]
            {
                "note", "ghichu"
            });
        }

        return map;
    }

    private static void SetIfMatch(Dictionary<string, int> map, string norm, int col, string key, string[] tokens)
    {
        if (map.ContainsKey(key))
        {
            return;
        }

        foreach (var token in tokens)
        {
            if (norm.Contains(token))
            {
                map[key] = col;
                return;
            }
        }
    }
}
