using System.Text.Json;
using ClosedXML.Excel;
using CongNoGolden.Infrastructure.Services;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public class ImportInvoiceTemplateParserTests
{
    [Fact]
    public void Invoice_Missing_IssueDate_Header_Does_Not_Parse_From_First_Column()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Invoice");
        WriteHeaderMissingIssueDate(sheet);

        sheet.Cell(2, 1).Value = "2025-01-01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = "INV-01";
        sheet.Cell(2, 4).Value = 1000;

        var rows = ImportInvoiceTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid());

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusError, rows[0].ValidationStatus);

        var messages = ReadMessages(rows[0].ValidationMessages);
        Assert.Contains("ISSUE_DATE_REQUIRED", messages);
    }

    [Fact]
    public void Invoice_IssueDate_String_DdMmYyyy_Is_Parsed()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Invoice");
        WriteFullHeader(sheet);

        sheet.Cell(2, 1).Value = "SELLER01";
        sheet.Cell(2, 2).Value = "CUST01";
        sheet.Cell(2, 3).Value = "Buyer A";
        sheet.Cell(2, 4).Value = "01GTKT";
        sheet.Cell(2, 5).Value = "AA/23E";
        sheet.Cell(2, 6).Value = "INV001";
        sheet.Cell(2, 7).Value = "15/02/2025";
        sheet.Cell(2, 8).Value = 100;
        sheet.Cell(2, 9).Value = 10;
        sheet.Cell(2, 10).Value = 110;

        var rows = ImportInvoiceTemplateParser.ParseSimpleTemplate(sheet, Guid.NewGuid());

        Assert.Single(rows);
        Assert.Equal(ImportStagingHelpers.StatusOk, rows[0].ValidationStatus);

        Assert.Equal("2025-02-15", ReadRawString(rows[0].RawData, "issue_date"));
    }

    private static void WriteHeaderMissingIssueDate(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "SellerTaxCode";
        sheet.Cell(1, 2).Value = "CustomerTaxCode";
        sheet.Cell(1, 3).Value = "InvoiceNo";
        sheet.Cell(1, 4).Value = "TotalAmount";
    }

    private static void WriteFullHeader(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "SellerTaxCode";
        sheet.Cell(1, 2).Value = "CustomerTaxCode";
        sheet.Cell(1, 3).Value = "CustomerName";
        sheet.Cell(1, 4).Value = "InvoiceTemplateCode";
        sheet.Cell(1, 5).Value = "InvoiceSeries";
        sheet.Cell(1, 6).Value = "InvoiceNo";
        sheet.Cell(1, 7).Value = "NgayPhatHanh";
        sheet.Cell(1, 8).Value = "RevenueExclVAT";
        sheet.Cell(1, 9).Value = "VatAmount";
        sheet.Cell(1, 10).Value = "TotalAmount";
        sheet.Cell(1, 11).Value = "Note";
    }

    private static IReadOnlyList<string> ReadMessages(string? raw)
    {
        return JsonSerializer.Deserialize<string[]>(raw ?? "[]") ?? Array.Empty<string>();
    }

    private static string? ReadRawString(string? raw, string property)
    {
        using var doc = JsonDocument.Parse(raw ?? "{}");
        return doc.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
    }
}
