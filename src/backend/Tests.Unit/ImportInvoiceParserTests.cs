using System.Text.Json;
using ClosedXML.Excel;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class ImportInvoiceParserTests
{
    [Fact]
    public void Missing_Buyer_Tax_Returns_Error()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Report");
        WriteSellerHeader(sheet);
        WriteInvoiceHeaders(sheet);
        WriteInvoiceRow(sheet, 7, "1", "Buyer A", string.Empty, "INV001", 100, 10);

        var rows = ImportInvoiceParser.ParseReportDetail(sheet, Guid.NewGuid());

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusError, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.Contains("BUYER_TAX_REQUIRED", messages);
    }

    [Fact]
    public void Duplicate_Rows_Are_Skipped()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Report");
        WriteSellerHeader(sheet);
        WriteInvoiceHeaders(sheet);
        WriteInvoiceRow(sheet, 7, "1", "Buyer A", "0101", "INV001", 100, 10);
        WriteInvoiceRow(sheet, 8, "2", "Buyer A", "0101", "INV001", 100, 10);

        var rows = ImportInvoiceParser.ParseReportDetail(sheet, Guid.NewGuid());

        Assert.Equal(2, rows.Count);
        Assert.Equal("INSERT", rows[0].ActionSuggestion);
        Assert.Equal("SKIP", rows[1].ActionSuggestion);
        Assert.Equal(ImportStagingHelpers.StatusWarn, rows[1].ValidationStatus);

        var messages = ReadMessages(rows[1].ValidationMessages);
        Assert.Contains("DUP_IN_FILE", messages);
    }

    private static void WriteSellerHeader(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "MaSoThue";
        sheet.Cell(1, 2).Value = "0102030405";
    }

    private static void WriteInvoiceHeaders(IXLWorksheet sheet)
    {
        sheet.Cell(5, 1).Value = "STT";
        sheet.Cell(5, 2).Value = "BuyerName";
        sheet.Cell(5, 3).Value = "TaxCode";
        sheet.Cell(5, 4).Value = "RevenueExcludingVAT";
        sheet.Cell(5, 5).Value = "VatAmount";
        sheet.Cell(5, 6).Value = "Note";

        sheet.Cell(6, 7).Value = "KyHieuMau";
        sheet.Cell(6, 8).Value = "SoHieuHoaDon";
        sheet.Cell(6, 9).Value = "SoHoaDon";
        sheet.Cell(6, 10).Value = "NgayThangNamPhatHanh";
    }

    private static void WriteInvoiceRow(
        IXLWorksheet sheet,
        int row,
        string stt,
        string buyerName,
        string buyerTax,
        string invoiceNo,
        decimal revenue,
        decimal vat)
    {
        sheet.Cell(row, 1).Value = stt;
        sheet.Cell(row, 2).Value = buyerName;
        sheet.Cell(row, 3).Value = buyerTax;
        sheet.Cell(row, 4).Value = revenue;
        sheet.Cell(row, 5).Value = vat;
        sheet.Cell(row, 6).Value = "note";
        sheet.Cell(row, 7).Value = "01GTKT";
        sheet.Cell(row, 8).Value = "AA/23E";
        sheet.Cell(row, 9).Value = invoiceNo;
        sheet.Cell(row, 10).Value = new DateTime(2025, 1, 15);
    }

    private static IReadOnlyList<string> ReadMessages(string? raw)
    {
        return JsonSerializer.Deserialize<string[]>(raw ?? "[]") ?? Array.Empty<string>();
    }
}
