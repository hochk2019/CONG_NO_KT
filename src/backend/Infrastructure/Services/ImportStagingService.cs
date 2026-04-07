using ClosedXML.Excel;
using CongNoGolden.Application.Imports;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;

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
}
