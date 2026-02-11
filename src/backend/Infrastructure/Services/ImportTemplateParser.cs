using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using CongNoGolden.Infrastructure.Data.Entities;

namespace CongNoGolden.Infrastructure.Services;

public static class ImportTemplateParser
{
    public static List<ImportStagingRow> ParseSimpleTemplate(IXLWorksheet sheet, Guid batchId, ImportTemplateType type)
    {
        var header = sheet.FirstRowUsed();
        if (header is null)
        {
            return new List<ImportStagingRow>();
        }

        var map = BuildColumnMap(header, type);
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
            var amount = ImportStagingHelpers.ParseDecimal(GetCellCell(row, map, "amount"));
            var description = GetCell(row, map, "description");
            var documentNo = type == ImportTemplateType.Advance
                ? GetCell(row, map, "advance_no")
                : GetCell(row, map, "receipt_no");

            var messages = new List<string>();
            ImportStagingHelpers.ValidateRequired(seller, "SELLER_TAX_REQUIRED", messages);
            ImportStagingHelpers.ValidateRequired(customer, "CUSTOMER_TAX_REQUIRED", messages);
            if (amount <= 0)
            {
                messages.Add("AMOUNT_REQUIRED");
            }

            var raw = new Dictionary<string, object?>
            {
                ["seller_tax_code"] = seller,
                ["customer_tax_code"] = customer,
                ["amount"] = amount,
                ["description"] = description
            };
            if (type == ImportTemplateType.Advance)
            {
                raw["advance_no"] = string.IsNullOrWhiteSpace(documentNo) ? null : documentNo;
            }
            else
            {
                raw["receipt_no"] = string.IsNullOrWhiteSpace(documentNo) ? null : documentNo;
            }

            string dedupKey;
            if (type == ImportTemplateType.Advance)
            {
                var advanceDate = ImportStagingHelpers.ParseDate(GetCellCell(row, map, "advance_date"));
                if (advanceDate is null)
                {
                    messages.Add("ADVANCE_DATE_REQUIRED");
                }

                raw["advance_date"] = advanceDate?.ToString("yyyy-MM-dd");
                dedupKey = ImportStagingHelpers.BuildKey(seller, customer, advanceDate?.ToString() ?? string.Empty,
                    amount.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                var receiptDate = ImportStagingHelpers.ParseDate(GetCellCell(row, map, "receipt_date"));
                var appliedPeriod = ImportStagingHelpers.ParseDate(GetCellCell(row, map, "applied_period_start"));
                if (receiptDate is null)
                {
                    messages.Add("RECEIPT_DATE_REQUIRED");
                }
                if (appliedPeriod is null)
                {
                    messages.Add("APPLIED_PERIOD_REQUIRED");
                }

                var method = GetCell(row, map, "method");
                if (string.IsNullOrWhiteSpace(method))
                {
                    method = "BANK";
                }
                method = method.ToUpperInvariant();
                if (method is not ("BANK" or "CASH" or "OTHER"))
                {
                    messages.Add("METHOD_INVALID");
                    method = "BANK";
                }

                if (appliedPeriod is not null && appliedPeriod.Value.Day != 1)
                {
                    messages.Add("APPLIED_PERIOD_NOT_FIRST_DAY");
                    appliedPeriod = new DateOnly(appliedPeriod.Value.Year, appliedPeriod.Value.Month, 1);
                }

                raw["receipt_date"] = receiptDate?.ToString("yyyy-MM-dd");
                raw["applied_period_start"] = appliedPeriod?.ToString("yyyy-MM-dd");
                raw["method"] = method;

                dedupKey = ImportStagingHelpers.BuildKey(seller, customer, receiptDate?.ToString() ?? string.Empty,
                    appliedPeriod?.ToString() ?? string.Empty, amount.ToString(CultureInfo.InvariantCulture));
            }

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

    private static Dictionary<string, int> BuildColumnMap(IXLRow header, ImportTemplateType type)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in header.CellsUsed())
        {
            var norm = ImportStagingHelpers.Normalize(cell.GetString());
            if (string.IsNullOrWhiteSpace(norm))
            {
                continue;
            }

            SetIfMatch(map, norm, cell.Address.ColumnNumber, "seller_tax_code", new[] { "sellertaxcode", "mstban", "masothueban", "sellertax" });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "customer_tax_code", new[] { "customertaxcode", "mstmua", "masothuemua", "customertax" });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "amount", new[] { "amount", "sotien", "tienthu", "giatri" });
            SetIfMatch(map, norm, cell.Address.ColumnNumber, "description", new[] { "description", "ghichu", "note" });

            if (type == ImportTemplateType.Advance)
            {
                SetIfMatch(map, norm, cell.Address.ColumnNumber, "advance_no", new[] { "advanceno", "sochungtu", "soct", "sct", "documentno" });
                SetIfMatch(map, norm, cell.Address.ColumnNumber, "advance_date", new[] { "advancedate", "ngaytraho", "ngaytien" });
            }
            else
            {
                SetIfMatch(map, norm, cell.Address.ColumnNumber, "receipt_no", new[] { "receiptno", "sophieuthu", "sophieu", "sochungtu", "soct", "sct", "documentno" });
                SetIfMatch(map, norm, cell.Address.ColumnNumber, "receipt_date", new[] { "receiptdate", "ngaythu", "ngaytien" });
                SetIfMatch(map, norm, cell.Address.ColumnNumber, "applied_period_start", new[] { "appliedperiodstart", "kydoisoat", "periodstart" });
                SetIfMatch(map, norm, cell.Address.ColumnNumber, "method", new[] { "method", "hinhthuc", "phuongthuc" });
            }
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
