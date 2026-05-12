using ClosedXML.Excel;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ImportStagingService : IImportStagingService
{
    private readonly ConGNoDbContext _db;

    public ImportStagingService(ConGNoDbContext db)
    {
        _db = db;
    }

    public async Task<ImportStagingResult> StageAsync(Guid batchId, string type, Stream fileStream, CancellationToken ct)
    {
        using var workbook = new XLWorkbook(fileStream);
        var sheets = GetOrderedSheets(workbook.Worksheets);

        var rows = type switch
        {
            "INVOICE" => ParseInvoiceSheets(sheets, batchId),
            "ADVANCE" => ImportTemplateParser.ParseSimpleTemplate(sheets.First(), batchId, ImportTemplateType.Advance),
            "RECEIPT" => ImportTemplateParser.ParseSimpleTemplate(sheets.First(), batchId, ImportTemplateType.Receipt),
            _ => throw new InvalidOperationException("Unsupported import type")
        };

        await NormalizeKnownTaxCodesAsync(type, rows, ct);
        await ImportDuplicateGuard.MarkDatabaseDuplicatesAsync(_db, type, rows, ct);

        _db.ImportStagingRows.AddRange(rows);
        await _db.SaveChangesAsync(ct);

        var total = rows.Count;
        var ok = rows.Count(r => r.ValidationStatus == ImportStagingHelpers.StatusOk);
        var warn = rows.Count(r => r.ValidationStatus == ImportStagingHelpers.StatusWarn);
        var error = rows.Count(r => r.ValidationStatus == ImportStagingHelpers.StatusError);

        return new ImportStagingResult(total, ok, warn, error);
    }

    private static List<IXLWorksheet> GetOrderedSheets(IXLWorksheets worksheets)
    {
        var list = worksheets.ToList();
        var export = list.FirstOrDefault(w => w.Name.Equals("ExportData", StringComparison.OrdinalIgnoreCase));
        if (export is null)
        {
            return list;
        }

        list.Remove(export);
        list.Insert(0, export);
        return list;
    }

    private static List<ImportStagingRow> ParseInvoiceSheets(
        IReadOnlyList<IXLWorksheet> sheets,
        Guid batchId)
    {
        foreach (var sheet in sheets)
        {
            var reportRows = ImportInvoiceParser.ParseReportDetail(sheet, batchId);
            if (reportRows.Count > 0)
            {
                return reportRows;
            }
        }

        foreach (var sheet in sheets)
        {
            var templateRows = ImportInvoiceTemplateParser.ParseSimpleTemplate(sheet, batchId);
            if (templateRows.Count > 0)
            {
                return templateRows;
            }
        }

        throw new InvalidOperationException("Không tìm thấy dữ liệu hóa đơn trong file.");
    }

    private async Task NormalizeKnownTaxCodesAsync(
        string type,
        IReadOnlyList<ImportStagingRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0 || type is not ("ADVANCE" or "RECEIPT"))
        {
            return;
        }

        var sellerCodes = ExtractRawCodes(rows, "seller_tax_code");
        var customerCodes = ExtractRawCodes(rows, "customer_tax_code");

        var sellerMap = await ResolveKnownTaxCodesAsync(
            sellerCodes,
            candidates => _db.Sellers
                .AsNoTracking()
                .Where(s => candidates.Contains(s.SellerTaxCode))
                .Select(s => s.SellerTaxCode)
                .ToListAsync(ct));

        var customerMap = await ResolveKnownTaxCodesAsync(
            customerCodes,
            candidates => _db.Customers
                .AsNoTracking()
                .Where(c => candidates.Contains(c.TaxCode))
                .Select(c => c.TaxCode)
                .ToListAsync(ct));

        foreach (var row in rows)
        {
            var raw = JsonNode.Parse(row.RawData)?.AsObject();
            if (raw is null)
            {
                continue;
            }

            var changed = TryNormalizeRawCode(raw, "seller_tax_code", sellerMap);
            changed |= TryNormalizeRawCode(raw, "customer_tax_code", customerMap);
            if (!changed)
            {
                continue;
            }

            row.RawData = raw.ToJsonString();
            row.DedupKey = BuildSimpleDocumentDedupKey(type, raw, row.RowNo);
        }
    }

    private static IReadOnlyCollection<string> ExtractRawCodes(
        IEnumerable<ImportStagingRow> rows,
        string property)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var raw = JsonNode.Parse(row.RawData)?.AsObject();
            var value = ReadRawString(raw, property);
            if (!string.IsNullOrWhiteSpace(value))
            {
                codes.Add(value);
            }
        }

        return codes;
    }

    private static async Task<Dictionary<string, string>> ResolveKnownTaxCodesAsync(
        IReadOnlyCollection<string> rawCodes,
        Func<List<string>, Task<List<string>>> queryKnownCodes)
    {
        var candidateCodes = rawCodes
            .SelectMany(GetTaxCodeCandidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidateCodes.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var known = (await queryKnownCodes(candidateCodes)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawCode in rawCodes)
        {
            if (known.Contains(rawCode))
            {
                result[rawCode] = rawCode;
                continue;
            }

            var resolved = GetTaxCodeCandidates(rawCode).FirstOrDefault(known.Contains);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                result[rawCode] = resolved;
            }
        }

        return result;
    }

    private static IEnumerable<string> GetTaxCodeCandidates(string rawCode)
    {
        var code = rawCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            yield break;
        }

        yield return code;
        if (!code.All(char.IsDigit))
        {
            yield break;
        }

        if (code.Length < 10)
        {
            yield return code.PadLeft(10, '0');
        }

        if (code.Length < 13)
        {
            yield return code.PadLeft(13, '0');
        }
    }

    private static bool TryNormalizeRawCode(
        JsonObject raw,
        string property,
        IReadOnlyDictionary<string, string> resolvedCodes)
    {
        var current = ReadRawString(raw, property);
        if (string.IsNullOrWhiteSpace(current) ||
            !resolvedCodes.TryGetValue(current, out var resolved) ||
            string.Equals(current, resolved, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        raw[property] = resolved;
        return true;
    }

    private static string BuildSimpleDocumentDedupKey(string type, JsonObject raw, int rowNo)
    {
        var seller = ReadRawString(raw, "seller_tax_code") ?? string.Empty;
        var customer = ReadRawString(raw, "customer_tax_code") ?? string.Empty;
        var documentNo = type == "ADVANCE"
            ? ReadRawString(raw, "advance_no")
            : ReadRawString(raw, "receipt_no");

        return string.IsNullOrWhiteSpace(documentNo)
            ? ImportStagingHelpers.BuildKey(seller, customer, $"missing-document-{rowNo}")
            : ImportStagingHelpers.BuildKey(seller, customer, documentNo);
    }

    private static string? ReadRawString(JsonObject? raw, string property)
    {
        return raw is not null && raw.TryGetPropertyValue(property, out var value)
            ? value?.ToString()
            : null;
    }
}
