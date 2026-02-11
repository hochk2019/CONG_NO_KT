using System.Text.Json;
using ClosedXML.Excel;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

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

        if (type == "INVOICE")
        {
            await MarkInvoiceDuplicatesAsync(rows, ct);
        }

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

    private async Task MarkInvoiceDuplicatesAsync(List<ImportStagingRow> rows, CancellationToken ct)
    {
        var keys = rows
            .Select(row => TryBuildInvoiceKey(row.RawData))
            .Where(key => key is not null)
            .Select(key => key!.Value)
            .Distinct()
            .ToList();

        if (keys.Count == 0)
        {
            return;
        }

        var existing = await LoadExistingInvoiceKeysAsync(keys, ct);
        if (existing.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            var key = TryBuildInvoiceKey(row.RawData);
            if (key is null)
            {
                continue;
            }

            if (!existing.Contains(key.Value))
            {
                continue;
            }

            var messages = ParseMessages(row.ValidationMessages);
            if (!messages.Contains("DUP_IN_DB"))
            {
                messages.Add("DUP_IN_DB");
            }

            row.ValidationMessages = JsonSerializer.Serialize(messages);
            row.ValidationStatus = ImportStagingHelpers.GetStatus(messages);
            row.ActionSuggestion = "SKIP";
        }
    }

    private async Task<HashSet<InvoiceKey>> LoadExistingInvoiceKeysAsync(
        IReadOnlyList<InvoiceKey> keys,
        CancellationToken ct)
    {
        var sellerCodes = keys.Select(k => k.SellerTaxCode).Distinct().ToList();
        var customerCodes = keys.Select(k => k.CustomerTaxCode).Distinct().ToList();
        var invoiceNos = keys.Select(k => k.InvoiceNo).Distinct().ToList();
        var issueDates = keys.Select(k => k.IssueDate).Distinct().ToList();
        var seriesList = keys.Select(k => k.InvoiceSeries).Distinct().ToList();

        var existing = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.DeletedAt == null)
            .Where(i =>
                sellerCodes.Contains(i.SellerTaxCode) &&
                customerCodes.Contains(i.CustomerTaxCode) &&
                invoiceNos.Contains(i.InvoiceNo) &&
                issueDates.Contains(i.IssueDate) &&
                seriesList.Contains(i.InvoiceSeries ?? string.Empty))
            .Select(i => new
            {
                i.SellerTaxCode,
                i.CustomerTaxCode,
                i.InvoiceSeries,
                i.InvoiceNo,
                i.IssueDate
            })
            .ToListAsync(ct);

        return new HashSet<InvoiceKey>(existing.Select(i => new InvoiceKey(
            NormalizeKeyPart(i.SellerTaxCode),
            NormalizeKeyPart(i.CustomerTaxCode),
            NormalizeKeyPart(i.InvoiceSeries ?? string.Empty),
            NormalizeKeyPart(i.InvoiceNo),
            i.IssueDate)));
    }

    private static InvoiceKey? TryBuildInvoiceKey(string rawData)
    {
        using var doc = JsonDocument.Parse(rawData);
        var raw = doc.RootElement;

        var seller = NormalizeKeyPart(ImportCommitJson.GetString(raw, "seller_tax_code"));
        var customer = NormalizeKeyPart(ImportCommitJson.GetString(raw, "customer_tax_code"));
        var invoiceNo = NormalizeKeyPart(ImportCommitJson.GetString(raw, "invoice_no"));
        var series = NormalizeKeyPart(ImportCommitJson.GetString(raw, "invoice_series"));
        var issueDate = ImportCommitJson.GetDate(raw, "issue_date");

        if (string.IsNullOrWhiteSpace(seller) ||
            string.IsNullOrWhiteSpace(customer) ||
            string.IsNullOrWhiteSpace(invoiceNo) ||
            issueDate is null)
        {
            return null;
        }

        return new InvoiceKey(seller, customer, series, invoiceNo, issueDate.Value);
    }

    private static List<string> ParseMessages(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string NormalizeKeyPart(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private readonly record struct InvoiceKey(
        string SellerTaxCode,
        string CustomerTaxCode,
        string InvoiceSeries,
        string InvoiceNo,
        DateOnly IssueDate);
}
